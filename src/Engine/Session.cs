using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;

namespace Rap.Studio.Engine
{
    public enum SessionState { Idle, Arming, Recording, Finalizing }

    public class LyricLine
    {
        public string Text = "";
        public DateTime Time = DateTime.Now;
        public System.Windows.Media.Color Color = Colors.White;
    }

    public class RecordResult
    {
        public string Path = "";
        public double Seconds;
        public long Bytes;
        public int Frames;
        public double LatencyMs;
        public string Platform = "";
        public string Title = "";
    }

    /// <summary>
    /// 会话编排：读取浏览器状态 → 判断视频开始/结束 → 预录缓冲 → 音画合成录制 → 台词识别。
    /// </summary>
    public class Session : IDisposable
    {
        private readonly Config _cfg;
        private readonly LogBus _log;

        public SessionState State = SessionState.Idle;

        public event Action<SessionState, string> OnState;
        public event Action<string, bool> OnNotice;
        public event Action<float, float> OnLevel;
        public event Action<BrowserWindow> OnSnapshot;
        public event Action<LyricLine> OnLyric;
        public event Action<System.Windows.Media.Color> OnThemeColor;
        public event Action<RecordResult> OnSaved;
        public event Action<double> OnRecordTime;

        private Thread _tick;
        private Thread _capture;
        private volatile bool _running;
        private volatile bool _recording;

        private Thread _audioInit;
        private volatile bool _audioReady;
        private bool _audioSlowWarned;
        private DateTime _startAt;
        private MicRecorder _mic;
        private LoopbackCapture _loop;
        private OcrWorker _ocr;

        private readonly PcmQueue _micRing = new PcmQueue();
        private readonly PcmQueue _sysRing = new PcmQueue();

        private class FrameRec
        {
            public byte[] Jpeg;
            public int W, H;
            public long Ms;
        }
        private readonly List<FrameRec> _frames = new List<FrameRec>();

        private AviWriter _avi;
        private readonly object _aviLock = new object();
        private string _outPath = "";

        private int _rate = 48000;
        private int _fps = 12;
        private int _lagSamples;
        private int _audioPushed;
        private IntPtr _hwnd = IntPtr.Zero;
        private Stopwatch _recSw = new Stopwatch();
        private long _recStartMs;
        private long _tickCount;
        private int _loudMs;
        private int _quietMs;
        private BrowserWindow _lastBw = new BrowserWindow();
        private System.Windows.Media.Color _themeColor = Colors.White;
        private long _lastColorMs = -1000;

        // 延迟自动校准
        private readonly List<float> _micBins = new List<float>();
        private readonly List<float> _sysBins = new List<float>();
        private int _binSamples;
        private int _binCount;
        private long _micBinAcc;
        private long _sysBinAcc;
        private bool _calibDone;

        public string OutputPath { get { return _outPath; } }
        public double RecordSeconds { get { return _recSw.Elapsed.TotalSeconds; } }
        public bool IsRecording { get { return _recording; } }

        public Session(Config cfg, LogBus log)
        {
            _cfg = cfg;
            _log = log;
            _fps = cfg.Fps;
            _rate = cfg.SampleRate;
        }

        public void Start()
        {
            if (State != SessionState.Idle) return;
            _running = true;
            _audioReady = false;
            _audioSlowWarned = false;
            _startAt = DateTime.Now;
            _loudMs = 0;
            _quietMs = 0;
            _tickCount = 0;
            _frames.Clear();

            _audioInit = new Thread(delegate()
            {
                StartAudioGuarded();
            });
            _audioInit.IsBackground = true;
            _audioInit.Name = "audio-init";
            _audioInit.Start();

            _recSw.Reset();
            _capture = new Thread(CaptureLoop);
            _capture.IsBackground = true;
            _capture.Name = "capture";
            _capture.Start();

            _tick = new Thread(TickLoop);
            _tick.IsBackground = true;
            _tick.Name = "session-tick";
            _tick.Start();

            SetState(SessionState.Arming, "正在检测浏览器状态");
            StartOcr();
            _log.Info("开始检测：音频阈值 " + _cfg.SilenceThreshold.ToString("0.000") +
                      "，静音判定 " + _cfg.SilenceMs + "ms");
        }

        private void StartAudioDevices()
        {
            try
            {
                _mic = new MicRecorder();
                _mic.Data += OnMicData;
                if (_mic.Start(_cfg.MicDevice, _cfg.SampleRate))
                {
                    _rate = _mic.SampleRate;
                    _log.Good("麦克风已就绪（" + _mic.SampleRate + "Hz）");
                }
                else
                {
                    _log.Warn("麦克风打开失败：" + (_mic.LastError == null ? "未知原因" : _mic.LastError));
                    _rate = _cfg.SampleRate;
                }

                if (_cfg.CaptureSystemAudio)
                {
                    _loop = new LoopbackCapture(_rate);
                    _loop.Data += OnSysData;
                    bool ok = _loop.Start();
                    if (!ok)
                    {
                        try { _loop.Stop(); } catch { }
                        Thread.Sleep(200);
                        _loop = new LoopbackCapture(_rate);
                        _loop.Data += OnSysData;
                        ok = _loop.Start();
                    }
                    if (ok) _log.Good("系统音频回环已开启（收录视频原声）");
                    else _log.Warn("系统音频回环不可用：" + _loop.LastError + "（仅录制麦克风）");
                }
            }
            catch (Exception ex)
            {
                _log.Error("音频设备初始化异常：" + ex.Message);
            }
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        [System.Security.SecurityCritical]
        private void StartAudioGuarded()
        {
            try { StartAudioDevices(); }
            catch (Exception ex) { _log.Warn("音频初始化异常：" + ex.Message); }
            _audioReady = true;
        }

        private void StopAudioDevices()
        {
            try { if (_mic != null) { _mic.Data -= OnMicData; _mic.Stop(); _mic = null; } } catch { }
            try { if (_loop != null) { _loop.Data -= OnSysData; _loop.Stop(); _loop = null; } } catch { }
        }

        private void OnMicData(short[] data)
        {
            _micRing.Push(data);
            _micRing.TrimTo(_rate);
            if (_recording && !_calibDone) FeedBin(true, data);
        }

        private void OnSysData(short[] data)
        {
            _sysRing.Push(data);
            _sysRing.TrimTo(_rate);
            if (_recording && !_calibDone) FeedBin(false, data);
        }

        private void FeedBin(bool mic, short[] data)
        {
            if (_binSamples <= 0) _binSamples = Math.Max(64, _rate / 100);
            long acc = 0;
            int count = _binCount;
            for (int i = 0; i < data.Length; i++)
            {
                acc += (long)data[i] * data[i];
                count++;
                if (count >= _binSamples)
                {
                    float rms = (float)Math.Sqrt(acc / (double)count) / 32768f;
                    lock (_micBins)
                    {
                        if (mic) _micBins.Add(rms); else _sysBins.Add(rms);
                    }
                    acc = 0;
                    count = 0;
                }
            }
            if (mic) _micBinAcc = acc; else _sysBinAcc = acc;
            _binCount = count;
        }

        public void Stop()
        {
            if (State == SessionState.Idle) return;
            _running = false;
            if (_tick != null)
            {
                try { _tick.Join(800); } catch { }
                _tick = null;
            }
            FinishRecord("手动停止", true);
            if (_capture != null)
            {
                try { _capture.Join(900); } catch { }
                _capture = null;
            }
            if (_audioInit != null)
            {
                try { _audioInit.Join(1500); } catch { }
                _audioInit = null;
            }
            OcrWorker ocr = _ocr;
            _ocr = null;
            Thread closer = new Thread(delegate()
            {
                try { StopAudioDevices(); } catch { }
                try { if (ocr != null) ocr.Stop(); } catch { }
            });
            closer.IsBackground = true;
            closer.Name = "audio-close";
            closer.Start();
            _frames.Clear();
            SetState(SessionState.Idle, "已停止");
        }

        private void SetState(SessionState st, string msg)
        {
            State = st;
            Action<SessionState, string> h = OnState;
            if (h != null) Ui.Post(delegate { h(st, msg); });
        }

        private void Notify(string msg, bool good)
        {
            Action<string, bool> h = OnNotice;
            if (h != null) Ui.Post(delegate { h(msg, good); });
        }

        private void TickLoop()
        {
            while (_running)
            {
                try { Tick(); } catch { }
                Thread.Sleep(200);
            }
        }

        private void Tick()
        {
            _tickCount++;
            if (!_audioReady && !_audioSlowWarned && (DateTime.Now - _startAt).TotalSeconds > 9)
            {
                _audioSlowWarned = true;
                _log.Warn("音频设备响应较慢，录音可能稍后开始（不影响视频录制）");
            }
            try
            {
                float peak = AudioPeak.Read();

                if (_state_arming())
                {
                    if (peak > _cfg.SilenceThreshold) _loudMs += 200; else _loudMs = 0;
                    if (_loudMs >= _cfg.StartHoldMs)
                    {
                        BrowserWindow bw = BrowserWatcher.Find();
                        if (bw.Found) BeginRecord(bw);
                        else _log.Warn("检测到声音但未找到浏览器窗口，已忽略");
                    }
                }
                else if (State == SessionState.Recording)
                {
                    if (peak <= _cfg.SilenceThreshold) _quietMs += 200; else _quietMs = 0;
                    if (_quietMs >= _cfg.SilenceMs) FinishRecord("视频播放结束", false);
                    else
                    {
                        Action<double> h = OnRecordTime;
                        if (h != null) { double sec = _recSw.Elapsed.TotalSeconds; Ui.Post(delegate { h(sec); }); }
                    }
                }

                if (_tickCount % 3 == 0 || !_lastBw.Found)
                {
                    BrowserWindow bw = BrowserWatcher.Find();
                    _lastBw = bw;
                    Action<BrowserWindow> sh = OnSnapshot;
                    if (sh != null) Ui.Post(delegate { sh(bw); });
                }

                float micPeak = _mic != null ? _mic.LastPeak : 0f;
                float sysPeak = _loop != null ? _loop.LastPeak : Math.Min(1f, peak);
                Action<float, float> lh = OnLevel;
                if (lh != null) Ui.Post(delegate { lh(micPeak, sysPeak); });
            }
            catch { }
        }

        private bool _state_arming()
        {
            return State == SessionState.Arming;
        }

        private void BeginRecord(BrowserWindow bw)
        {
            try
            {
                if (_recording) return;
                if (!_cfg.AutoRecord)
                {
                    Notify("检测到视频开始（自动录制已关闭）", true);
                    return;
                }

                Directory.CreateDirectory(_cfg.OutDir);
                string safeTitle = bw.Platform;
                _outPath = Path.Combine(_cfg.OutDir,
                    "说唱记录_" + safeTitle + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".avi");

                Bitmap probe = WindowCapturer.Capture(bw.Hwnd, _cfg.MaxWidth);
                int w = probe != null ? probe.Width : 1280;
                int h = probe != null ? probe.Height : 720;
                if (probe != null) probe.Dispose();

                _hwnd = bw.Hwnd;
                _fps = Math.Min(_cfg.Fps, AppInfo.MaxFpsLimit);
                _rate = _mic != null && _mic.SampleRate > 0 ? _mic.SampleRate : _cfg.SampleRate;
                _lagSamples = (int)((long)_rate * _cfg.LatencyMs / 1000);

                lock (_aviLock)
                {
                    _avi = new AviWriter(_outPath, w, h, _fps, _rate);
                }

                // 延迟补偿：系统音频整体后移 lag 毫秒，让麦克风与人声起点对齐
                if (_lagSamples > 0)
                {
                    _sysRing.Push(new short[_lagSamples]);
                }

                // 预录画面 + 音频，实现"录制起点与视频起点完全一致"
                int frameCount = 0;
                List<FrameRec> pre = new List<FrameRec>();
                for (int i = 0; i < _frames.Count; i++)
                {
                    if (_frames[i].W == w && _frames[i].H == h) { pre.Add(_frames[i]); }
                }
                frameCount = pre.Count;
                int want = frameCount * (_rate / Math.Max(1, _fps));
                short[] mixPre = PullMixed(want);
                if (mixPre != null && mixPre.Length > 0) _avi.PushAudio(mixPre);
                int audioHave = mixPre == null ? 0 : mixPre.Length;
                if (audioHave < want)
                {
                    int missing = want - audioHave;
                    if (missing > 0 && missing < _rate * 3) _avi.PushAudio(new short[missing]);
                }
                for (int i = 0; i < pre.Count; i++)
                {
                    lock (_aviLock) { if (_avi != null) _avi.AddFrame(pre[i].Jpeg); }
                }
                _frames.Clear();

                _audioPushed = 0;
                _recSw.Reset();
                _recSw.Start();
                _recStartMs = _recSw.ElapsedMilliseconds;
                _quietMs = 0;
                _recording = true;

                lock (_micBins) { _micBins.Clear(); _sysBins.Clear(); }
                _calibDone = false;

                StartOcr();

                string msg = "检测到视频开始 · 正在录制" + (frameCount > 0 ? "（已回补 " + frameCount + " 帧预录）" : "");
                SetState(SessionState.Recording, msg);
                _log.Good(msg);
                _log.Info("输出文件：" + _outPath);
                Notify("开始录制：" + bw.Platform, true);
            }
            catch (Exception ex)
            {
                _log.Error("开始录制失败：" + ex.Message);
                Notify("开始录制失败：" + ex.Message, false);
            }
        }

        private void StartOcr()
        {
            if (!_cfg.OcrEnabled) return;
            PrepareLyric();
            try
            {
                if (_ocr == null || !_ocr.Ready) return;
                if (_ocrThread == null || !_ocrThread.IsAlive)
                {
                    _ocrThread = new Thread(OcrLoop);
                    _ocrThread.IsBackground = true;
                    _ocrThread.Name = "ocr";
                    _ocrThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Warn("台词线程启动失败：" + ex.Message);
            }
        }

        public event Action<string> OnOcrStatus;

        private Thread _ocrPrepThread;
        private volatile bool _ocrPrepared;
        private string _ocrStatus = "未启动";
        private string _lastLyricText = "";

        public bool OcrReady { get { return _ocr != null && _ocr.Ready; } }
        public string OcrStatusText { get { return _ocrStatus; } }

        /// <summary>预热台词引擎（在空闲期或点击开始检测时调用），进程与 WinRT 引擎一次拉起后常驻复用。</summary>
        public void PrepareLyric()
        {
            if (!_cfg.OcrEnabled) { ReportOcr("台词识别已在设置中关闭"); return; }
            if (_ocrPrepared && _ocr != null && _ocr.Ready) return;
            if (_ocrPrepThread != null && _ocrPrepThread.IsAlive) return;
            _ocrPrepThread = new Thread(PrepareLyricBody);
            _ocrPrepThread.IsBackground = true;
            _ocrPrepThread.Name = "ocr-prepare";
            _ocrPrepThread.Start();
        }

        private void PrepareLyricBody()
        {
            try
            {
                if (_ocr == null) _ocr = new OcrWorker();
                if (!_ocr.EnsureStarted())
                {
                    _ocrPrepared = false;
                    ReportOcr("台词引擎不可用：" + _ocr.Status);
                    _log.Warn("台词引擎不可用：" + _ocr.Status);
                    return;
                }
                _ocrPrepared = true;
                ReportOcr("台词引擎已就绪（" + _ocr.Language + "）");
                _log.Good("台词引擎已就绪（" + _ocr.Language + "），等待视频开始即可识别");
                _ocr.Preload();
            }
            catch (Exception ex)
            {
                _ocrPrepared = false;
                ReportOcr("台词引擎异常：" + ex.Message);
                _log.Warn("台词引擎异常：" + ex.Message);
            }
        }

        /// <summary>关闭时释放常驻 OCR 进程。</summary>
        public void ShutdownLyric()
        {
            _running = false;
            try { if (_ocr != null) { _ocr.Stop(); _ocr = null; } } catch { }
            _ocrPrepared = false;
        }

        private void ReportOcr(string msg)
        {
            _ocrStatus = msg;
            Action<string> h = OnOcrStatus;
            if (h != null) Ui.Post(delegate { h(msg); });
        }

        private Thread _ocrThread;

        private void OcrLoop()
        {
            while (_running)
            {
                if (!_cfg.OcrEnabled)
                {
                    Thread.Sleep(500);
                    continue;
                }
                if (_ocr == null || !_ocr.Ready)
                {
                    // 引擎还没热起来：顺手再拉起一次，成功后继续
                    if (!_ocrPrepared) PrepareLyric();
                    Thread.Sleep(500);
                    continue;
                }
                try
                {
                    IntPtr hwnd = _hwnd;
                    if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd))
                    {
                        BrowserWindow bw = BrowserWatcher.Find();
                        hwnd = bw.Hwnd;
                    }
                    if (hwnd == IntPtr.Zero) { Thread.Sleep(600); continue; }

                    Bitmap frame = WindowCapturer.Capture(hwnd, 1200);
                    if (frame != null)
                    {
                        using (Bitmap crop = SubtitleCropper.Crop(frame, _cfg.OcrRegion))
                        {
                            frame.Dispose();
                            if (crop != null)
                            {
                                string tmp = Path.Combine(Path.GetTempPath(), "rs_ocr.png");
                                crop.Save(tmp, System.Drawing.Imaging.ImageFormat.Png);
                                string text = _ocr.Recognize(tmp, 5000);
                                if (!string.IsNullOrEmpty(text))
                                {
                                    text = FixText(text);
                                    if (text.Trim().Length >= 1 && text != _lastLyricText)
                                    {
                                        _lastLyricText = text;
                                        LyricLine line = new LyricLine();
                                        line.Text = text;
                                        line.Time = DateTime.Now;
                                        line.Color = _themeColor;
                                        Action<LyricLine> h = OnLyric;
                                        if (h != null) Ui.Post(delegate { h(line); });
                                        _log.Info("台词：" + text);
                                    }
                                }
                                try { File.Delete(tmp); } catch { }
                            }
                        }
                    }
                }
                catch { }
                int wait = Math.Max(400, _cfg.OcrIntervalMs);
                Thread.Sleep(wait);
            }
        }

        private string FixText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string t = text;
            foreach (string pair in _cfg.OcrFix)
            {
                int idx = pair.IndexOf("=>");
                if (idx <= 0) continue;
                string from = pair.Substring(0, idx);
                string to = pair.Substring(idx + 2);
                if (from.Length > 0) t = t.Replace(from, to);
            }
            return t.Trim();
        }

        private short[] PullMixed(int want)
        {
            int n = Math.Min(Math.Min(_micRing.Count, _sysRing.Count), want);
            if (n <= 0) return null;
            short[] a = _micRing.Take(n);
            short[] b = _sysRing.Take(n);
            short[] mix = new short[n];
            for (int i = 0; i < n; i++)
            {
                int v = a[i] + b[i];
                if (v > 32000) v = 32000;
                if (v < -32000) v = -32000;
                mix[i] = (short)v;
            }
            return mix;
        }

        private void CaptureLoop()
        {
            Stopwatch sw = Stopwatch.StartNew();
            double interval = 1000.0 / Math.Max(1, _fps);
            long last = -1000;
            int prerollFrames = Math.Max(2, (int)((_cfg.Fps * 800) / 1000));

            while (_running)
            {
                try
                {
                    long now = sw.ElapsedMilliseconds;
                    double wait = interval - (now - last);
                    if (wait > 1) Thread.Sleep((int)wait);
                    now = sw.ElapsedMilliseconds;
                    if (now - last < interval * 0.6) continue;
                    last = now;

                    IntPtr hwnd = _hwnd;
                    if (!_recording)
                    {
                        BrowserWindow bw = BrowserWatcher.Find();
                        if (!bw.Found) { Thread.Sleep(300); continue; }
                        hwnd = bw.Hwnd;
                        _hwnd = hwnd;
                    }
                    if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) { Thread.Sleep(200); continue; }

                    Bitmap frame = WindowCapturer.Capture(hwnd, _cfg.MaxWidth);
                    if (frame == null) continue;
                    byte[] jpeg = WindowCapturer.EncodeJpeg(frame, _cfg.JpegQuality);

                    if (now - _lastColorMs > 420)
                    {
                        _lastColorMs = now;
                        System.Drawing.Color c = WindowCapturer.DominantColor(frame);
                        if (c.R + c.G + c.B > 60)
                        {
                            System.Windows.Media.Color mc = System.Windows.Media.Color.FromRgb(c.R, c.G, c.B);
                            if (mc != _themeColor)
                            {
                                _themeColor = mc;
                                Action<System.Windows.Media.Color> h = OnThemeColor;
                                if (h != null) Ui.Post(delegate { h(mc); });
                            }
                        }
                    }

                    if (_recording)
                    {
                        int due = (int)(now * _rate / 1000.0) - _audioPushed;
                        if (due > _rate / 3) due = _rate / 3;
                        if (due > 0)
                        {
                            short[] mix = PullMixed(due);
                            if (mix != null && mix.Length > 0) { _audioPushed += mix.Length; }
                            lock (_aviLock) { if (_avi != null) { if (mix != null) _avi.PushAudio(mix); _avi.AddFrame(jpeg); } }
                        }
                        else
                        {
                            lock (_aviLock) { if (_avi != null) _avi.AddFrame(jpeg); }
                        }
                        TryCalibrate();
                    }
                    else
                    {
                        FrameRec fr = new FrameRec();
                        fr.Jpeg = jpeg;
                        fr.W = frame.Width;
                        fr.H = frame.Height;
                        fr.Ms = now;
                        lock (_frames)
                        {
                            _frames.Add(fr);
                            while (_frames.Count > prerollFrames) _frames.RemoveAt(0);
                        }
                    }
                    frame.Dispose();
                }
                catch { Thread.Sleep(80); }
            }
        }

        /// <summary>用首个明显起音比较麦克风与系统音的时间差，估算麦克风链路延迟。</summary>
        private void TryCalibrate()
        {
            if (_calibDone || !_cfg.AutoCalibrate) return;
            if (_recSw.ElapsedMilliseconds < 2600) return;
            _calibDone = true;
            try
            {
                float[] micArr, sysArr;
                lock (_micBins)
                {
                    micArr = _micBins.ToArray();
                    sysArr = _sysBins.ToArray();
                }
                int sysOnset = -1, micOnset = -1;
                for (int i = 0; i < sysArr.Length; i++) if (sysArr[i] > 0.12f) { sysOnset = i; break; }
                for (int i = 0; i < micArr.Length; i++) if (micArr[i] > 0.12f) { micOnset = i; break; }
                if (sysOnset < 0 || micOnset < 0) return;
                int lag = micOnset - sysOnset;
                if (lag < 2 || lag > 45) return;   // 20~450ms 才算有效
                int lagMs = lag * 10;
                int dif = Math.Abs(lagMs - _cfg.LatencyMs);
                if (dif >= 20)
                {
                    _cfg.LatencyMs = lagMs;
                    _cfg.Save();
                    Notify("已估算麦克风延迟 " + lagMs + "ms，下次录制自动生效", true);
                    _log.Good("延迟自动校准：" + lagMs + "ms（本次已按 " + _cfg.LatencyMs + "ms 处理，下一段生效）");
                }
            }
            catch { }
        }

        private void FinishRecord(string reason, bool silent)
        {
            if (!_recording) return;
            _recording = false;
            AviWriter avi = null;
            lock (_aviLock)
            {
                avi = _avi;
                _avi = null;
            }
            double sec = _recSw.Elapsed.TotalSeconds;
            _recSw.Stop();

            if (avi != null)
            {
                try { avi.Close(); } catch { }
            }

            RecordResult r = new RecordResult();
            r.Path = _outPath;
            r.Seconds = sec;
            r.Frames = avi != null ? (int)avi.FramesWritten : 0;
            r.LatencyMs = _cfg.LatencyMs;
            r.Platform = _lastBw.Platform;
            r.Title = _lastBw.Title;
            try
            {
                if (File.Exists(_outPath)) r.Bytes = new FileInfo(_outPath).Length;
            }
            catch { }

            _log.Info("录制结束（" + reason + "）：" + sec.ToString("0.0") + " 秒，" + (r.Bytes / 1024 / 1024) + " MB");
            Action<RecordResult> h = OnSaved;
            if (h != null) Ui.Post(delegate { h(r); });
            if (!silent) Notify("视频结束，已保存录音", true);

            if (_running) SetState(SessionState.Arming, "已保存，继续等待下一个视频");
        }

        public void Dispose()
        {
            try { Stop(); } catch { }
        }
    }
}
