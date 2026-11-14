using System;
using System.IO;

namespace Rap.Studio
{
    /// <summary>应用标识与版本信息（开源版 / 自用版由编译符号 EDITION_PRO 区分）。</summary>
    public static class AppInfo
    {
        public const string DisplayName = "说唱主播助手";
        public const string EnglishName = "Rap Streamer Assistant";
        public const string PkgId = "RapStreamerTool";

#if EDITION_PRO
        public const bool IsPro = true;
        public const string Edition = "自用版";
        public const string Version = "1.2.0";
        public const int MaxFpsLimit = 60;
        public const string Channel = "PRO";
#else
        public const bool IsPro = false;
        public const string Edition = "开源版";
        public const string Version = "1.0.0";
        public const int MaxFpsLimit = 15;
        public const string Channel = "OPEN";
#endif

        public static string VersionText
        {
            get { return "v" + Version + " · " + Edition; }
        }

        public static string ShortVersion
        {
            get { return "v" + Version; }
        }

        /// <summary>配置与日志目录：%AppData%\说唱主播助手</summary>
        public static string DataDir
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DisplayName);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>默认录制目录：我的视频\说唱主播助手</summary>
        public static string DefaultOutDir
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), DisplayName);
                return dir;
            }
        }

        public static string BuildStamp
        {
            get { return "构建于 2026-11-14 · 通道 " + Channel; }
        }
    }
}
