using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;

namespace Rap.Studio.Engine
{
    /// <summary>常驻的 PowerShell 5.1 OCR 工作进程（Windows 自带中文识别，无需联网与额外安装）。</summary>
    public class OcrWorker : IDisposable
    {
        private Process _proc;
        private StreamWriter _stdin;
        private BlockingCollection<string> _lines;
        private Thread _reader;
        private volatile bool _ready;
        private string _scriptPath;
        public string Status = "未启动";
        public string Language = "";

        private const string ScriptBody = @"
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}
try {
  [System.Reflection.Assembly]::LoadFrom(""$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll"") | Out-Null
  Add-Type -AssemblyName System.Drawing
} catch { [Console]::Out.WriteLine(""FATAL|运行库加载失败""); exit }
$engine = $null
try { $engine = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]::TryCreateFromUserProfileLanguages() } catch { }
if ($null -eq $engine) { [Console]::Out.WriteLine(""FATAL|系统 OCR 引擎不可用""); exit }
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
$ocrT = [Windows.Media.Ocr.OcrResult, Windows.Foundation, ContentType=WindowsRuntime]
$pf = [Windows.Graphics.Imaging.BitmapPixelFormat, Windows.Graphics.Imaging, ContentType=WindowsRuntime]
$sbt = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics.Imaging, ContentType=WindowsRuntime]
$bufExt = [System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions]
function Await($op, $type) { $asTask.MakeGenericMethod($type).Invoke($null, @($op)).Result }
[Console]::Out.WriteLine(""READY|"" + $engine.RecognizerLanguage.LanguageTag)
[Console]::Out.Flush()
while ($true) {
  $line = [Console]::In.ReadLine()
  if ($null -eq $line) { break }
  $line = $line.Trim()
  if ($line -eq 'QUIT') { break }
  if ($line.Length -eq 0) { continue }
  try {
    $bmp = [System.Drawing.Bitmap]::FromFile($line)
    $w = $bmp.Width; $h = $bmp.Height
    $conv = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($conv)
    $g.DrawImage($bmp, 0, 0, $w, $h)
    $g.Dispose(); $bmp.Dispose()
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $conv.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $h)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $conv.UnlockBits($data); $conv.Dispose()
    $buf = $bufExt::AsBuffer($bytes)
    $sb = $sbt::CreateCopyFromBuffer($buf, $pf::Bgra8, $w, $h)
    $res = Await ($engine.RecognizeAsync($sb)) $ocrT
    $text = ($res.Text -replace ' ', '') -replace ""[`r`n]"", ' | '
    [Console]::Out.WriteLine(""OK|"" + $text)
  } catch {
    [Console]::Out.WriteLine(""ERR|"" + $_.Exception.Message.Replace(""`r"", '').Replace(""`n"", ' '))
  }
  [Console]::Out.Flush()
}
";

        public bool Ready { get { return _ready; } }

        private readonly object _startLock = new object();
        private readonly object _callLock = new object();
        private Thread _preload;

        /// <summary>幂等启动：已就绪直接返回 true，避免重复拉起 PowerShell 进程。</summary>
        public bool EnsureStarted()
        {
            lock (_startLock)
            {
                if (_ready && _proc != null && !_proc.HasExited && _lines != null) return true;
                _ready = false;
                return Start();
            }
        }

        /// <summary>空闲期预热：拉起进程后用一张合成图跑一次识别，把 WinRT 首次调用的开销提前付掉。</summary>
        public void Preload()
        {
            if (_preload != null && _preload.IsAlive) return;
            _preload = new Thread(PreloadBody);
            _preload.IsBackground = true;
            _preload.Name = "ocr-preload";
            _preload.Start();
        }

        private void PreloadBody()
        {
            if (!EnsureStarted()) return;
            string p = Path.Combine(Path.GetTempPath(), "rs_ocr_warm.png");
            try
            {
                using (Bitmap bmp = new Bitmap(240, 72))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.White);
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        using (Font f = new Font("Microsoft YaHei", 26, FontStyle.Bold))
                        using (SolidBrush br = new SolidBrush(Color.Black))
                        {
                            g.DrawString("台词预热", f, br, 8, 12);
                        }
                    }
                    bmp.Save(p, ImageFormat.Png);
                }
                string warm = Recognize(p, 8000);
                if (warm == null)
                {
                    // 首次识别超时说明管道有问题，直接重启进程，避免后续每次都超时
                    Stop();
                }
            }
            catch { }
            finally
            {
                try { File.Delete(p); } catch { }
            }
        }

        public OcrWorker()
        {
            _scriptPath = Path.Combine(AppInfo.DataDir, "ocr_worker.ps1");
            try
            {
                File.WriteAllText(_scriptPath, ScriptBody, new UTF8Encoding(true));
            }
            catch { }
        }

        public bool Start()
        {
            Stop();
            try
            {
                string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"System32\WindowsPowerShell\v1.0\powershell.exe");
                if (!File.Exists(exe)) exe = "powershell.exe";

                ProcessStartInfo si = new ProcessStartInfo();
                si.FileName = exe;
                si.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + _scriptPath + "\"";
                si.UseShellExecute = false;
                si.CreateNoWindow = true;
                si.RedirectStandardInput = true;
                si.RedirectStandardOutput = true;
                si.RedirectStandardError = true;
                si.StandardOutputEncoding = Encoding.UTF8;
                si.StandardErrorEncoding = Encoding.UTF8;

                _proc = new Process();
                _proc.StartInfo = si;
                _proc.Start();
                _stdin = _proc.StandardInput;
                _stdin.AutoFlush = true;
                _lines = new BlockingCollection<string>();

                _reader = new Thread(ReadLoop);
                _reader.IsBackground = true;
                _reader.Name = "ocr-reader";
                _reader.Start();

                string first = WaitLine(6000);
                if (first == null)
                {
                    Status = "OCR 进程无响应";
                    Stop();
                    return false;
                }
                if (first.StartsWith("READY|"))
                {
                    _ready = true;
                    Language = first.Substring(6);
                    Status = "就绪 · " + Language;
                    return true;
                }
                Status = first.Replace("FATAL|", "");
                Stop();
                return false;
            }
            catch (Exception ex)
            {
                Status = "启动失败：" + ex.Message;
                Stop();
                return false;
            }
        }

        private void ReadLoop()
        {
            try
            {
                string line;
                while ((line = _proc.StandardOutput.ReadLine()) != null)
                {
                    _lines.Add(line);
                }
            }
            catch { }
        }

        private string WaitLine(int timeoutMs)
        {
            if (_lines == null) return null;
            string v;
            try
            {
                if (_lines.TryTake(out v, timeoutMs)) return v;
            }
            catch { }
            return null;
        }

        /// <summary>识别图片，返回文本（多行用 " | " 连接）；失败返回 null。</summary>
        public string Recognize(string pngPath, int timeoutMs)
        {
            if (!_ready) return null;
            lock (_callLock)
            {
                if (!_ready) return null;
                try
                {
                    _stdin.WriteLine(pngPath);
                    _stdin.Flush();
                    string res = WaitLine(timeoutMs);
                    if (res == null)
                    {
                        // 超时：管道里可能残留着上一张图的结果，重启进程避免串行错位
                        _ready = false;
                        return null;
                    }
                    if (res.StartsWith("OK|")) return res.Substring(3);
                    if (res.StartsWith("ERR|")) return null;
                    return null;
                }
                catch { _ready = false; return null; }
            }
        }

        public void Stop()
        {
            _ready = false;
            try { if (_stdin != null) { _stdin.WriteLine("QUIT"); _stdin.Flush(); } } catch { }
            try { if (_proc != null && !_proc.HasExited) _proc.Kill(); } catch { }
            try { if (_proc != null) _proc.Dispose(); } catch { }
            _proc = null;
            _stdin = null;
            _lines = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
