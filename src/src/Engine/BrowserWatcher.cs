using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Rap.Studio.Engine
{
    public class BrowserWindow
    {
        public IntPtr Hwnd;
        public string Title = "";
        public string Process = "";
        public string Platform = "";
        public int Area;
        public double ElapsedSec;

        public bool Found { get { return Hwnd != IntPtr.Zero; } }
    }

    /// <summary>枚举浏览器窗口并按标题识别平台。</summary>
    public static class BrowserWatcher
    {
        private static readonly string[] Processes = new string[] {
            "chrome", "msedge", "firefox", "brave", "opera", "vivaldi",
            "360se", "360chrome", "360se6", "qqbrowser", "sogouexplorer", "2345explorer",
            "iexplore", "bilibili", "douyin", "kuaishou", "xiaohongshu", "youtube"
        };

        private struct Cand
        {
            public IntPtr Hwnd;
            public string Title;
            public string Proc;
            public string Cls;
            public int Area;
            public bool Foreground;
        }

        private static string ProcessName(uint pid)
        {
            try
            {
                return Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
            }
            catch { return ""; }
        }

        private static BrowserWindow _cache;
        private static long _cacheAt = -100000;
        private static readonly object _cacheLock = new object();

        /// <summary>带缓存的扫描（900ms 内复用上次结果，避免高频枚举窗口拖慢系统）。</summary>
        public static BrowserWindow Find()
        {
            lock (_cacheLock)
            {
                long now = Environment.TickCount;
                if (_cache != null && now - _cacheAt < 900 && now >= _cacheAt) return _cache;
                long probe = now;
                BrowserWindow bw = Scan();
                _cache = bw;
                _cacheAt = probe;
                return bw;
            }
        }

        /// <summary>强制重新扫描，忽略缓存。</summary>
        public static BrowserWindow FindNow()
        {
            lock (_cacheLock)
            {
                _cache = Scan();
                _cacheAt = Environment.TickCount;
                return _cache;
            }
        }

        private static BrowserWindow Scan()
        {
            BrowserWindow result = new BrowserWindow();
            try
            {
                IntPtr fg = Native.GetForegroundWindow();
                List<Cand> list = new List<Cand>();
                Native.EnumWindows(delegate(IntPtr hwnd, IntPtr l)
                {
                    try
                    {
                        if (!Native.IsWindowVisible(hwnd)) return true;
                        if (Native.IsIconic(hwnd)) return true;
                        string title = Native.WindowTitle(hwnd);
                        if (string.IsNullOrEmpty(title)) return true;
                        uint pid;
                        Native.GetWindowThreadProcessId(hwnd, out pid);
                        string proc = ProcessName(pid);
                        bool known = false;
                        for (int i = 0; i < Processes.Length; i++)
                        {
                            if (proc == Processes[i] || proc.StartsWith(Processes[i])) { known = true; break; }
                        }
                        string cls = Native.WindowClass(hwnd);
                        bool browserClass = cls == "Chrome_WidgetWin_1" || cls == "MozillaWindowClass" ||
                                            cls == "ApplicationFrameWindow" || cls.StartsWith("Chrome_WidgetWin");
                        if (!known && !browserClass) return true;
                        Native.RECT rc;
                        if (!Native.GetWindowRect(hwnd, out rc)) return true;
                        int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
                        if (w < 320 || h < 240) return true;

                        Cand c = new Cand();
                        c.Hwnd = hwnd;
                        c.Title = title;
                        c.Proc = proc;
                        c.Cls = cls;
                        c.Area = w * h;
                        c.Foreground = (hwnd == fg);
                        list.Add(c);
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);

                if (list.Count == 0) return result;

                Cand best = list[0];
                for (int i = 1; i < list.Count; i++)
                {
                    Cand c = list[i];
                    int score = c.Area;
                    if (c.Foreground) score += 4000000;
                    int bestScore = best.Area + (best.Foreground ? 4000000 : 0);
                    if (score > bestScore) best = c;
                }

                result.Hwnd = best.Hwnd;
                result.Title = best.Title;
                result.Process = best.Proc;
                result.Area = best.Area;
                result.Platform = DetectPlatform(best.Title, best.Proc);
            }
            catch { }
            return result;
        }

        public static string DetectPlatform(string title, string proc)
        {
            string t = (title == null ? "" : title).ToLowerInvariant();
            string p = proc == null ? "" : proc.ToLowerInvariant();
            if (t.Contains("哔哩哔哩") || t.Contains("bilibili") || t.Contains("b站") || p.Contains("bilibili")) return "哔哩哔哩";
            if (t.Contains("抖音") || t.Contains("douyin") || p.Contains("douyin")) return "抖音";
            if (t.Contains("快手") || t.Contains("kuaishou") || p.Contains("kuaishou")) return "快手";
            if (t.Contains("小红书") || p.Contains("xiaohongshu")) return "小红书";
            if (t.Contains("youtube")) return "YouTube";
            if (t.Contains("西瓜视频")) return "西瓜视频";
            if (t.Contains("腾讯视频")) return "腾讯视频";
            if (t.Contains("爱奇艺") || t.Contains("iqiyi")) return "爱奇艺";
            if (t.Contains("优酷") || t.Contains("youku")) return "优酷";
            if (t.Contains("微博")) return "微博";
            return "浏览器";
        }

        /// <summary>粗略判断当前窗口是否是"视频播放页"（用于提示，不阻断检测）。</summary>
        public static bool LooksLikeVideoPage(string title)
        {
            if (string.IsNullOrEmpty(title)) return false;
            string t = title.ToLowerInvariant();
            if (t.Contains("哔哩哔哩") && !t.Contains("首页") && !t.Contains("发现")) return true;
            if (t.Contains("视频") || t.Contains("播放") || t.Contains("video")) return true;
            if (t.Contains("抖音") || t.Contains("快手") || t.Contains("youtube")) return true;
            return false;
        }
    }
}
