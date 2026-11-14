using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Rap.Studio
{
    public class AppHost
    {
        public Config Cfg;
        public LogBus Log;
        public Rap.Studio.Engine.Session Session;
        public MainWindow Window;

        public AppHost(Config cfg, LogBus log)
        {
            Cfg = cfg;
            Log = log;
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                Boot(args);
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(AppInfo.DataDir);
                    File.AppendAllText(Path.Combine(AppInfo.DataDir, "crash.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + ex.ToString() + Environment.NewLine + Environment.NewLine,
                        System.Text.Encoding.UTF8);
                }
                catch { }
                MessageBox.Show("启动失败：" + ex.Message + Environment.NewLine + "详细信息已写入 crash.log。",
                    AppInfo.DisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void Trace(string m)
        {
            try
            {
                Directory.CreateDirectory(AppInfo.DataDir);
                File.AppendAllText(Path.Combine(AppInfo.DataDir, "trace.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + m + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private static void Boot(string[] args)
        {
            try
            {
                Rap.Studio.Engine.Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch
            {
                try { Rap.Studio.Engine.Native.SetProcessDPIAware(); } catch { }
            }

            bool created = true;
            Mutex mutex = new Mutex(true, "RapStreamerAssistant_Mutex_" + AppInfo.Channel, out created);
            if (!created)
            {
                MessageBox.Show("「" + AppInfo.DisplayName + "」已经在运行中，请切换到已打开的窗口。",
                    AppInfo.DisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            Ui.D = Dispatcher.CurrentDispatcher;
            app.DispatcherUnhandledException += delegate(object s2, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                Trace("UI-EX: " + e.Exception.ToString());
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s3, UnhandledExceptionEventArgs e)
            {
                Trace("APP-EX: " + e.ExceptionObject.ToString());
            };

            Config cfg = Config.Load();
            LogBus log = new LogBus();
            AppHost host = new AppHost(cfg, log);
            host.Session = new Rap.Studio.Engine.Session(cfg, log);

            bool skipIntro = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--skipintro") skipIntro = true;
            }

            if (!cfg.IntroShown && !skipIntro)
            {
                IntroWindow intro = new IntroWindow(host);
                if (HasArg(args, "--shot"))
                {
                    intro.Show();
                    DispatcherTimer it = new DispatcherTimer();
                    it.Interval = TimeSpan.FromMilliseconds(900);
                    it.Tick += delegate(object s4, EventArgs e4)
                    {
                        it.Stop();
                        SaveShot(intro, Path.Combine(ShotDir, "ui_intro.png"));
                        intro.Close();
                    };
                    it.Start();
                }
                else
                {
                    intro.ShowDialog();
                }
                cfg.IntroShown = true;
                cfg.Save();
            }

            MainWindow win = new MainWindow(host);
            host.Window = win;
            // 空闲期先把台词引擎拉起来，点开始检测后第一次字幕不再等 PowerShell 冷启动
            try { host.Session.PrepareLyric(); } catch { }
            if (HasArg(args, "--selftest")) RunSelfTest(host);
            if (HasArg(args, "--shot")) RunShots(win);
            app.Run(win);
        }

        private static string ShotDir
        {
            get
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shots");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static bool HasArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++) if (args[i] == name) return true;
            return false;
        }

        private static void SaveShot(Window w, string path)
        {
            try
            {
                int wd = (int)Math.Round(w.ActualWidth);
                int ht = (int)Math.Round(w.ActualHeight);
                if (wd <= 0 || ht <= 0) return;
                RenderTargetBitmap rtb = new RenderTargetBitmap(wd, ht, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(w);
                System.Windows.Media.Imaging.PngBitmapEncoder enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (FileStream fs = File.Create(path)) enc.Save(fs);
                Trace("shot " + path);
            }
            catch (Exception ex) { Trace("shot-fail " + ex.Message); }
        }

        /// <summary>无人值守自检：启动引擎 6 秒后停止，把日志写入 trace。</summary>
        private static DispatcherTimer _stTimer;

        private static void RunSelfTest(AppHost host)
        {
            try
            {
                host.Session.Start();
                DispatcherTimer t = new DispatcherTimer();
                _stTimer = t;
                t.Interval = TimeSpan.FromSeconds(6);
                t.Tick += delegate(object s, EventArgs e)
                {
                    t.Stop();
                    try { host.Session.Stop(); } catch (Exception ex) { Trace("selftest-stop-fail " + ex.Message); }
                    foreach (Rap.Studio.LogEntry line in host.Log.Items) Trace("log " + line.Text);
                    Environment.Exit(0);
                };
                t.Start();
            }
            catch (Exception ex) { Trace("selftest-fail " + ex.ToString()); }
        }

        private static void RunShots(MainWindow win)
        {
            string[] keys = new string[] { "home", "record", "lyric", "settings", "about" };
            int i = 0;
            DispatcherTimer t = new DispatcherTimer();
            t.Interval = TimeSpan.FromMilliseconds(900);
            t.Tick += delegate(object s, EventArgs e)
            {
                if (i >= keys.Length)
                {
                    t.Stop();
                    Application.Current.Shutdown();
                    return;
                }
                string key = keys[i];
                i++;
                win.ShowPage(key);
                DispatcherTimer t2 = new DispatcherTimer();
                t2.Interval = TimeSpan.FromMilliseconds(520);
                t2.Tick += delegate(object s2, EventArgs e2)
                {
                    t2.Stop();
                    SaveShot(win, Path.Combine(ShotDir, "ui_" + key + ".png"));
                };
                t2.Start();
            };
            win.Loaded += delegate { t.Start(); };
        }
    }
}
