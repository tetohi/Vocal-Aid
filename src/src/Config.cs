using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Rap.Studio
{
    /// <summary>设置项：以极简 key=value 形式持久化到 %AppData%\说唱主播助手\config.ini</summary>
    public class Config
    {
        public bool IntroShown = false;
        public int MicDevice = -1;              // -1 表示系统默认录音设备
        public int SampleRate = 48000;
        public int LatencyMs = 80;              // 麦克风延迟补偿
        public bool AutoCalibrate = true;       // 录制开始时自动估算麦克风延迟
        public bool CaptureSystemAudio = true;  // 采集系统回环音频（视频原声）
        public int Fps = 12;
        public int JpegQuality = 72;
        public int MaxWidth = 1280;
        public string OutDir = "";
        public double SilenceThreshold = 0.035;
        public int SilenceMs = 2200;
        public int StartHoldMs = 500;
        public bool OcrEnabled = true;
        public int OcrIntervalMs = 1100;
        public int OcrRegion = 0;               // 0=下方字幕带 1=中部字幕带
        public bool ThemeFollow = true;
        public bool AutoRecord = true;
        public bool KeepLog = true;
        public List<string> OcrFix = new List<string>();   // 自用版：错=>对

        private static string FilePath
        {
            get { return Path.Combine(AppInfo.DataDir, "config.ini"); }
        }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq).Trim();
                        string v = line.Substring(eq + 1).Trim();
                        c.Set(k, v);
                    }
                }
            }
            catch { }
            if (string.IsNullOrEmpty(c.OutDir)) c.OutDir = AppInfo.DefaultOutDir;
            if (c.Fps < 4) c.Fps = 4;
            if (c.Fps > AppInfo.MaxFpsLimit) c.Fps = AppInfo.MaxFpsLimit;
            if (c.JpegQuality < 40) c.JpegQuality = 40;
            if (c.JpegQuality > 95) c.JpegQuality = 95;
            if (c.SampleRate != 44100 && c.SampleRate != 48000) c.SampleRate = 48000;
            if (c.LatencyMs < 0) c.LatencyMs = 0;
            if (c.LatencyMs > 1000) c.LatencyMs = 1000;
            if (c.SilenceMs < 500) c.SilenceMs = 500;
            if (c.StartHoldMs < 100) c.StartHoldMs = 100;
            if (c.OcrIntervalMs < 400) c.OcrIntervalMs = 400;
            return c;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 说唱主播助手 配置文件（" + AppInfo.Edition + "）");
                sb.AppendLine("introShown=" + B(IntroShown));
                sb.AppendLine("micDevice=" + MicDevice);
                sb.AppendLine("sampleRate=" + SampleRate);
                sb.AppendLine("latencyMs=" + LatencyMs);
                sb.AppendLine("autoCalibrate=" + B(AutoCalibrate));
                sb.AppendLine("captureSystemAudio=" + B(CaptureSystemAudio));
                sb.AppendLine("fps=" + Fps);
                sb.AppendLine("jpegQuality=" + JpegQuality);
                sb.AppendLine("maxWidth=" + MaxWidth);
                sb.AppendLine("outDir=" + OutDir);
                sb.AppendLine("silenceThreshold=" + SilenceThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("silenceMs=" + SilenceMs);
                sb.AppendLine("startHoldMs=" + StartHoldMs);
                sb.AppendLine("ocrEnabled=" + B(OcrEnabled));
                sb.AppendLine("ocrIntervalMs=" + OcrIntervalMs);
                sb.AppendLine("ocrRegion=" + OcrRegion);
                sb.AppendLine("themeFollow=" + B(ThemeFollow));
                sb.AppendLine("autoRecord=" + B(AutoRecord));
                sb.AppendLine("keepLog=" + B(KeepLog));
                foreach (string f in OcrFix) sb.AppendLine("ocrFix=" + f);
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string B(bool v) { return v ? "1" : "0"; }

        private static bool Bool(string v)
        {
            v = v.Trim().ToLowerInvariant();
            return v == "1" || v == "true" || v == "yes" || v == "on";
        }

        private void Set(string k, string v)
        {
            switch (k)
            {
                case "introShown": IntroShown = Bool(v); break;
                case "micDevice": MicDevice = Int(v, -1); break;
                case "sampleRate": SampleRate = Int(v, 48000); break;
                case "latencyMs": LatencyMs = Int(v, 80); break;
                case "autoCalibrate": AutoCalibrate = Bool(v); break;
                case "captureSystemAudio": CaptureSystemAudio = Bool(v); break;
                case "fps": Fps = Int(v, 12); break;
                case "jpegQuality": JpegQuality = Int(v, 72); break;
                case "maxWidth": MaxWidth = Int(v, 1280); break;
                case "outDir": OutDir = v; break;
                case "silenceThreshold":
                    double d = 0.035;
                    double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d);
                    SilenceThreshold = d;
                    break;
                case "silenceMs": SilenceMs = Int(v, 2200); break;
                case "startHoldMs": StartHoldMs = Int(v, 500); break;
                case "ocrEnabled": OcrEnabled = Bool(v); break;
                case "ocrIntervalMs": OcrIntervalMs = Int(v, 1100); break;
                case "ocrRegion": OcrRegion = Int(v, 0); break;
                case "themeFollow": ThemeFollow = Bool(v); break;
                case "autoRecord": AutoRecord = Bool(v); break;
                case "keepLog": KeepLog = Bool(v); break;
                case "ocrFix": OcrFix.Add(v); break;
            }
        }

        private static int Int(string v, int def)
        {
            int i;
            return int.TryParse(v, out i) ? i : def;
        }
    }
}
