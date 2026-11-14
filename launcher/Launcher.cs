using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VocalAid.Setup
{
    public static class Cfg
    {
        public const string Version = "1.2.0";
        public const string AppName = "说唱主播助手";
        public static readonly string[] Mirrors = new string[]
        {
            "https://codeload.github.com/tetohi/Vocal-Aid/zip/refs/heads/main",
            "https://api.github.com/repos/tetohi/Vocal-Aid/zipball/main",
            "https://codeload.github.com/tetohi/Vocal-Aid/zip/refs/tags/v1.2.0"
        };
        public const string FileName = "说唱主播助手.exe";
        public const string SelfName = "说唱主播助手_下载引导.exe";

        public static string Root()
        {
            string env = Environment.GetEnvironmentVariable("VOCALAID_ROOT");
            if (!string.IsNullOrEmpty(env)) { if (!Directory.Exists(env)) Directory.CreateDirectory(env); return env; }
            try
            {
                if (Directory.Exists("E:\\"))
                {
                    string p = "E:\\说唱主播助手";
                    if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                    return p;
                }
            }
            catch { }
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "说唱主播助手");
            if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            return d;
        }
    }

    public static class Trace
    {
        private static readonly string LogPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "说唱主播助手_下载引导", "setup.log");

        public static void Write(string s)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + "\r\n", Encoding.UTF8);
            }
            catch { }
        }
    }

    public static class Installer
    {
        public static void Download(string url, string file, Action<long, long> onProgress)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Vocal-Aid-Setup/" + Cfg.Version;
            req.Timeout = 20000;
            req.ReadWriteTimeout = 40000;
            req.AllowAutoRedirect = true;
            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (Stream rs = res.GetResponseStream())
            using (FileStream fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long total = res.ContentLength;
                byte[] buf = new byte[81920];
                long got = 0;
                int n;
                while ((n = rs.Read(buf, 0, buf.Length)) > 0)
                {
                    fs.Write(buf, 0, n);
                    got += n;
                    if (onProgress != null) onProgress(got, total);
                }
                fs.Flush();
            }
        }

        public static string FetchZip(Action<long, long> onProgress, Action<string> log)
        {
            Exception last = null;
            string tmp = Path.Combine(Path.GetTempPath(), "vocalaid_" + DateTime.Now.Ticks + ".zip");
            foreach (string u in Cfg.Mirrors)
            {
                try
                {
                    Trace.Write("下载源：" + u);
                    if (log != null) log("连接：" + u);
                    Download(AntiCache(u), tmp, onProgress);
                    FileInfo fi = new FileInfo(tmp);
                    Trace.Write("已下载 " + fi.Length + "B");
                    if (fi.Length < 20000) throw new Exception("文件过小：" + fi.Length + "B");
                    return tmp;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Trace.Write("该源失败：" + ex.Message);
                    if (log != null) log("该源失败，换下一个");
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                }
            }
            throw new Exception("下载失败：" + (last == null ? "未知原因" : last.Message));
        }

        private static string AntiCache(string u)
        {
            if (u.IndexOf("codeload.", StringComparison.OrdinalIgnoreCase) < 0) return u;
            return u + (u.IndexOf("?") < 0 ? "?" : "&") + "nc=" + DateTime.Now.Ticks;
        }

        public static bool Extract(string zipPath, string root, Action<string> log)
        {
            bool ok = false;
            using (ZipArchive za = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry e in za.Entries)
                {
                    string full = e.FullName.Replace('\\', '/');
                    if (full.EndsWith("/")) continue;
                    if (full.IndexOf("/dist/", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string name = Path.GetFileName(full);
                    string target = null;
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (name.IndexOf("Pro", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        target = Path.Combine(root, Cfg.FileName);
                    }
                    else if (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        target = Path.Combine(root, "使用说明.txt");
                    }
                    if (target == null) continue;
                    e.ExtractToFile(target, true);
                    Trace.Write("已写入 " + target);
                    if (log != null) log("已写入：" + target);
                    if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ok = true;
                }
            }
            return ok;
        }

        public static bool HasLocal()
        {
            return File.Exists(Path.Combine(Cfg.Root(), Cfg.FileName));
        }

        /// <summary>用独立的 PowerShell 进程写快捷方式，避开 COM 在无消息泵线程上卡死。</summary>
        public static void Link(string lnkName, string target, string desc)
        {
            try
            {
                if (!File.Exists(target)) return;
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnk = Path.Combine(desktop, lnkName + ".lnk");
                string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\WindowsPowerShell\\v1.0\\powershell.exe");
                if (!File.Exists(ps)) return;
                string script =
                    "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + lnk.Replace("'", "''") + "');" +
                    "$s.TargetPath='" + target.Replace("'", "''") + "';" +
                    "$s.WorkingDirectory='" + Path.GetDirectoryName(target).Replace("'", "''") + "';" +
                    "$s.Description='" + desc.Replace("'", "''") + "';" +
                    "$s.IconLocation='" + (target + ",0").Replace("'", "''") + "';" +
                    "$s.Save()";
                ProcessStartInfo psi = new ProcessStartInfo(ps, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process p = Process.Start(psi);
                p.WaitForExit(10000);
                Trace.Write("快捷方式：" + lnk);
            }
            catch (Exception ex) { Trace.Write("快捷方式失败：" + ex.Message); }
        }

        public static void Shortcuts()
        {
            string root = Cfg.Root();
            Link("说唱主播助手", Path.Combine(root, Cfg.FileName), "说唱主播助手 v" + Cfg.Version);
        }
    }

    public class Win : Window
    {
        private TextBlock _status;
        private ProgressBar _bar;
        private TextBox _log;
        private Button _main;
        private TextBlock _state;
        private CheckBox _autoRun;

        public Win()
        {
            Title = "说唱主播助手 · 下载引导 v" + Cfg.Version;
            Width = 700;
            Height = 440;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF5, 0xF7));
            FontFamily = new FontFamily("Microsoft YaHei UI");
            ResizeMode = ResizeMode.CanMinimize;
            Opacity = 0;
            Build();
            Loaded += delegate
            {
                DoubleAnimation a = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280));
                BeginAnimation(OpacityProperty, a);
            };
        }

        private SolidColorBrush B(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }

        private void Build()
        {
            StackPanel root = new StackPanel();
            root.Margin = new Thickness(26, 20, 26, 18);

            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border logo = new Border();
            logo.Width = 44;
            logo.Height = 44;
            logo.CornerRadius = new CornerRadius(10);
            logo.HorizontalAlignment = HorizontalAlignment.Left;
            LinearGradientBrush lb = new LinearGradientBrush();
            lb.StartPoint = new Point(0, 0);
            lb.EndPoint = new Point(1, 1);
            lb.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0x6A, 0x3D), 0));
            lb.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0x3B, 0x1E), 1));
            logo.Background = lb;
            TextBlock mic = new TextBlock();
            mic.Text = "♪";
            mic.FontSize = 22;
            mic.Foreground = Brushes.White;
            mic.HorizontalAlignment = HorizontalAlignment.Center;
            mic.VerticalAlignment = VerticalAlignment.Center;
            logo.Child = mic;
            head.Children.Add(logo);

            StackPanel ht = new StackPanel();
            ht.Margin = new Thickness(10, 2, 0, 0);
            Grid.SetColumn(ht, 1);
            TextBlock t1 = new TextBlock();
            t1.Text = "说唱主播助手 · 下载引导";
            t1.FontSize = 19;
            t1.FontWeight = FontWeights.Bold;
            t1.Foreground = B("#1F2328");
            ht.Children.Add(t1);
            TextBlock t2 = new TextBlock();
            t2.Text = "v" + Cfg.Version + " · 从 GitHub 直连下载，安装到 E 盘并生成桌面快捷方式";
            t2.FontSize = 11.5;
            t2.Foreground = B("#6E7681");
            t2.Margin = new Thickness(0, 3, 0, 0);
            ht.Children.Add(t2);
            head.Children.Add(ht);
            root.Children.Add(head);

            Border card = new Border();
            card.CornerRadius = new CornerRadius(10);
            card.BorderThickness = new Thickness(1);
            card.BorderBrush = B("#E5E7EB");
            card.Background = Brushes.White;
            card.Margin = new Thickness(0, 16, 0, 0);
            StackPanel cs = new StackPanel();
            cs.Margin = new Thickness(16, 14, 16, 14);

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border dot = new Border();
            dot.Width = 8;
            dot.Height = 8;
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.CornerRadius = new CornerRadius(4);
            dot.Background = B("#FF4C2B");
            row.Children.Add(dot);

            StackPanel s = new StackPanel();
            Grid.SetColumn(s, 1);
            TextBlock a = new TextBlock();
            a.Text = "说唱主播助手";
            a.FontSize = 13.5;
            a.FontWeight = FontWeights.SemiBold;
            a.Foreground = B("#1F2328");
            s.Children.Add(a);
            _state = new TextBlock();
            _state.FontSize = 11;
            _state.Margin = new Thickness(0, 2, 0, 0);
            s.Children.Add(_state);
            row.Children.Add(s);
            cs.Children.Add(row);

            TextBlock path = new TextBlock();
            path.Text = "安装目录：" + Cfg.Root();
            path.FontSize = 11;
            path.Foreground = B("#6E7681");
            path.Margin = new Thickness(0, 12, 0, 0);
            cs.Children.Add(path);
            card.Child = cs;
            root.Children.Add(card);

            _bar = new ProgressBar();
            _bar.Height = 6;
            _bar.Margin = new Thickness(0, 16, 0, 0);
            _bar.Minimum = 0;
            _bar.Maximum = 100;
            _bar.Foreground = B("#FF4C2B");
            _bar.Background = B("#E5E7EB");
            _bar.BorderThickness = new Thickness(0);
            root.Children.Add(_bar);

            _status = new TextBlock();
            _status.Text = "准备就绪，点击下方按钮开始下载";
            _status.FontSize = 12;
            _status.Foreground = B("#6E7681");
            _status.Margin = new Thickness(0, 8, 0, 0);
            root.Children.Add(_status);

            StackPanel btns = new StackPanel();
            btns.Orientation = Orientation.Horizontal;
            btns.Margin = new Thickness(0, 14, 0, 0);

            _main = new Button();
            _main.Content = "一键下载并安装";
            StyleBtn(_main, true, 176);
            _main.Click += delegate { StartInstall(); };
            btns.Children.Add(_main);

            Button openDir = new Button();
            openDir.Content = "打开安装目录";
            StyleBtn(openDir, false, 132);
            openDir.Margin = new Thickness(10, 0, 0, 0);
            openDir.Click += delegate { try { Process.Start("explorer.exe", Cfg.Root()); } catch { } };
            btns.Children.Add(openDir);

            Button relink = new Button();
            relink.Content = "重建快捷方式";
            StyleBtn(relink, false, 132);
            relink.Margin = new Thickness(10, 0, 0, 0);
            relink.Click += delegate
            {
                Installer.Shortcuts();
                Note("桌面快捷方式已创建");
            };
            btns.Children.Add(relink);
            root.Children.Add(btns);

            _autoRun = new CheckBox();
            _autoRun.Content = "下载安装完成后自动启动程序，并关闭本引导";
            _autoRun.IsChecked = true;
            _autoRun.FontSize = 12;
            _autoRun.Foreground = B("#1F2328");
            _autoRun.Margin = new Thickness(2, 12, 0, 0);
            _autoRun.Cursor = System.Windows.Input.Cursors.Hand;
            root.Children.Add(_autoRun);

            _log = new TextBox();
            _log.IsReadOnly = true;
            _log.AcceptsReturn = true;
            _log.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _log.Height = 96;
            _log.Margin = new Thickness(0, 14, 0, 0);
            _log.FontFamily = new FontFamily("Consolas");
            _log.FontSize = 10.5;
            _log.Background = Brushes.White;
            _log.Foreground = B("#4A5056");
            _log.BorderBrush = B("#E5E7EB");
            _log.Padding = new Thickness(8, 6, 8, 6);
            root.Children.Add(_log);

            Content = root;
            Refresh();
        }

        private void Refresh()
        {
            bool has = Installer.HasLocal();
            _state.Text = has ? "已安装，可随时重新下载覆盖" : "未安装";
            _state.Foreground = has ? B("#18A058") : B("#C2410C");
        }

        private void StyleBtn(Button b, bool primary, double w)
        {
            b.Width = w;
            b.Height = 34;
            b.FontSize = 12.5;
            b.BorderThickness = new Thickness(primary ? 0 : 1);
            b.Cursor = System.Windows.Input.Cursors.Hand;
            if (primary)
            {
                b.Background = B("#FF4C2B");
                b.Foreground = Brushes.White;
                b.BorderBrush = B("#FF4C2B");
            }
            else
            {
                b.Background = Brushes.White;
                b.Foreground = B("#1F2328");
                b.BorderBrush = B("#E5E7EB");
            }
        }

        private void Note(string t)
        {
            _status.Text = t;
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + t + "\r\n");
            _log.ScrollToEnd();
        }

        private void LaunchAndQuit()
        {
            try
            {
                string exe = Path.Combine(Cfg.Root(), Cfg.FileName);
                if (File.Exists(exe))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(exe);
                    psi.WorkingDirectory = Cfg.Root();
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                    Trace.Write("已启动程序：" + exe);
                }
            }
            catch (Exception ex) { Trace.Write("启动程序失败：" + ex.Message); }
            Dispatcher.BeginInvoke(new Action(delegate { Close(); }));
        }

        private void StartInstall()
        {
            _main.IsEnabled = false;
            _bar.Value = 0;
            Note("开始连接 GitHub 下载源…");
            Thread t = new Thread(delegate()
            {
                string zip = null;
                try
                {
                    zip = Installer.FetchZip(delegate(long got, long total)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            _bar.Value = total > 0 ? (got * 100.0 / total * 0.8) : 40;
                            _status.Text = "正在下载 " + (got / 1024) + " KB" + (total > 0 ? " / " + (total / 1024) + " KB" : "");
                        }));
                    }, delegate(string s) { Dispatcher.BeginInvoke(new Action(delegate { Note(s); })); });

                    Dispatcher.BeginInvoke(new Action(delegate { _bar.Value = 85; Note("下载完成，正在解压安装…"); }));
                    bool ok = Installer.Extract(zip, Cfg.Root(), delegate(string s) { Dispatcher.BeginInvoke(new Action(delegate { Note(s); })); });
                    if (!ok) throw new Exception("压缩包里没找到程序文件");

                    Dispatcher.BeginInvoke(new Action(delegate { _bar.Value = 92; Note("正在创建桌面快捷方式…"); }));
                    Installer.Shortcuts();

                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _bar.Value = 100;
                        Refresh();
                        Note("安装完成");
                        _status.Text = "安装完成，正在启动程序…";
                        _main.Content = "重新下载";
                        _main.IsEnabled = true;
                        if (_autoRun.IsChecked == true) LaunchAndQuit();
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        Note("失败：" + ex.Message);
                        _status.Text = "下载没成功，可换网络后重试";
                        _main.IsEnabled = true;
                    }));
                }
                finally
                {
                    try { if (zip != null && File.Exists(zip)) File.Delete(zip); } catch { }
                }
            });
            t.IsBackground = true;
            t.Start();
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            ServicePointManager.Expect100Continue = false;
            Trace.Write("启动，参数：" + (args.Length > 0 ? args[0] : "无"));

            if (args.Length > 0 && args[0] == "--auto")
            {
                string zip = Installer.FetchZip(null, delegate(string s) { Trace.Write(s); });
                bool ok = Installer.Extract(zip, Cfg.Root(), delegate(string s) { Trace.Write(s); });
                Installer.Shortcuts();
                Trace.Write("完成 ok=" + ok + " 目录=" + Cfg.Root());
                Environment.Exit(0);
            }

            Application app = new Application();
            app.Run(new Win());
        }
    }
}
