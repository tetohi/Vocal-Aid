using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace VocalAid.Setup
{
    public static class Cfg
    {
        public const string Version = "1.2.0";
        public const string Repo = "tetohi/Vocal-Aid";
        public static readonly string[] Mirrors = new string[]
        {
            "https://codeload.github.com/tetohi/Vocal-Aid/zip/refs/heads/main",
            "https://api.github.com/repos/tetohi/Vocal-Aid/zipball/main",
            "https://codeload.github.com/tetohi/Vocal-Aid/zip/refs/tags/v1.2.0"
        };
        public static readonly string ProName = "说唱主播助手_自用版.exe";
        public static readonly string OpenName = "说唱主播助手_开源版.exe";

        public static string Root()
        {
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
        private static readonly string Path =
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "说唱主播助手_下载引导", "setup.log");

        public static void Write(string s)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(Path, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + "\r\n", Encoding.UTF8);
            }
            catch { }
        }
    }

    public static class Installer
    {
        public static int Download(string url, string file, Action<long, long> onProgress)
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
                return (int)got;
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
                    if (log != null) log("下载源：" + u);
                    Download(u, tmp, onProgress);
                    FileInfo fi = new FileInfo(tmp);
                    Trace.Write("已下载：" + fi.Length + "B");
                    if (fi.Length < 20000) throw new Exception("文件过小：" + fi.Length + "B");
                    return tmp;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Trace.Write("该源失败：" + ex);
                    if (log != null) log("该源失败：" + ex.Message);
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                }
            }
            throw new Exception("全部下载源都失败：" + (last == null ? "" : last.Message));
        }

        public static void Extract(string zipPath, string root, Action<string> log)
        {
            using (ZipArchive za = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry e in za.Entries)
                {
                    string full = e.FullName.Replace('\\', '/');
                    if (full.EndsWith("/")) continue;
                    string name = Path.GetFileName(full);
                    string target = null;
                    if (full.IndexOf("/dist/", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            if (name.IndexOf("Pro", StringComparison.OrdinalIgnoreCase) >= 0) target = Path.Combine(root, Cfg.ProName);
                            else if (name.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0) target = Path.Combine(root, Cfg.OpenName);
                            else target = Path.Combine(root, name);
                        }
                        else if (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) target = Path.Combine(root, "使用说明.txt");
                    }
                    if (target == null) continue;
                    e.ExtractToFile(target, true);
                    if (log != null) log("已写入：" + target);
                }
            }
        }

        public static bool HasLocal(bool pro)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] names = pro ? new string[] { Cfg.ProName, "Vocal-Aid_Pro_v1.2.0.exe" } : new string[] { Cfg.OpenName, "Vocal-Aid_Open_v1.2.0.exe" };
            foreach (string n in names) { if (File.Exists(Path.Combine(baseDir, n))) return true; }
            string e = Path.Combine("E:\\说唱主播助手", pro ? Cfg.ProName : Cfg.OpenName);
            return File.Exists(e);
        }

        public static void Link(string lnkName, string target, string desc)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnk = Path.Combine(desktop, lnkName + ".lnk");
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object sh = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { desc });
                st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            catch { }
        }

        public static void Shortcuts()
        {
            string root = Cfg.Root();
            Link("说唱主播助手 · 自用版", Path.Combine(root, Cfg.ProName), "自用增强版 60fps");
            Link("说唱主播助手 · 开源版", Path.Combine(root, Cfg.OpenName), "开源分发版 15fps");
            string self = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            Link("说唱主播助手 · 下载引导", self, "下载与安装引导");
        }
    }

    public class Win : Window
    {
        private TextBlock _status;
        private ProgressBar _bar;
        private TextBox _log;
        private Button _main;
        private Button _openDir;
        private TextBlock _proState;
        private TextBlock _openState;

        public Win()
        {
            Title = "说唱主播助手 · 下载引导 v" + Cfg.Version;
            Width = 760;
            Height = 470;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF5, 0xF7));
            ResizeMode = ResizeMode.CanMinimize;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            SizeToContent = SizeToContent.Manual;
            Build();
            Loaded += delegate { Intro(); };
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

            Border card = Card();
            card.Margin = new Thickness(0, 16, 0, 0);
            StackPanel cs = new StackPanel();
            cs.Margin = new Thickness(16, 14, 16, 14);

            Grid row1 = Row("自用增强版", "60fps 录制 · 台词引擎预热 · 完整功能");
            _proState = (TextBlock)((StackPanel)((Grid)row1).Children[1]).Children[1];
            cs.Children.Add(row1);
            cs.Children.Add(Spacer(10));
            Grid row2 = Row("开源分发版", "15fps 录制 · 开源协议 MIT");
            _openState = (TextBlock)((StackPanel)((Grid)row2).Children[1]).Children[1];
            cs.Children.Add(row2);

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
            _bar.Value = 0;
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

            _openDir = new Button();
            _openDir.Content = "打开安装目录";
            StyleBtn(_openDir, false, 132);
            _openDir.Margin = new Thickness(10, 0, 0, 0);
            _openDir.Click += delegate { try { System.Diagnostics.Process.Start("explorer.exe", Cfg.Root()); } catch { } };
            btns.Children.Add(_openDir);

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

            _log = new TextBox();
            _log.IsReadOnly = true;
            _log.AcceptsReturn = true;
            _log.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _log.Height = 108;
            _log.Margin = new Thickness(0, 14, 0, 0);
            _log.FontFamily = new FontFamily("Consolas");
            _log.FontSize = 10.5;
            _log.Background = Brushes.White;
            _log.Foreground = B("#4A5056");
            _log.BorderBrush = B("#E5E7EB");
            _log.Padding = new Thickness(8, 6, 8, 6);
            root.Children.Add(_log);

            Content = root;
            RefreshStates();
        }

        private void RefreshStates()
        {
            _proState.Text = Installer.HasLocal(true) ? "已安装" : "未安装";
            _proState.Foreground = Installer.HasLocal(true) ? B("#18A058") : B("#C2410C");
            _openState.Text = Installer.HasLocal(false) ? "已安装" : "未安装";
            _openState.Foreground = Installer.HasLocal(false) ? B("#18A058") : B("#C2410C");
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

        private Border Card()
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(10);
            b.BorderThickness = new Thickness(1);
            b.BorderBrush = B("#E5E7EB");
            b.Background = Brushes.White;
            return b;
        }

        private FrameworkElement Spacer(double h)
        {
            return new Border { Height = h, Background = Brushes.Transparent };
        }

        private Grid Row(string title, string desc)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border dot = new Border();
            dot.Width = 8;
            dot.Height = 8;
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.CornerRadius = new CornerRadius(4);
            dot.Background = B("#FF4C2B");
            g.Children.Add(dot);

            StackPanel s = new StackPanel();
            Grid.SetColumn(s, 1);
            TextBlock a = new TextBlock();
            a.Text = title;
            a.FontSize = 13.5;
            a.FontWeight = FontWeights.SemiBold;
            a.Foreground = B("#1F2328");
            s.Children.Add(a);
            TextBlock b2 = new TextBlock();
            b2.Text = desc;
            b2.FontSize = 11;
            b2.Foreground = B("#6E7681");
            b2.Margin = new Thickness(0, 2, 0, 0);
            s.Children.Add(b2);
            g.Children.Add(s);
            return g;
        }

        private void Intro()
        {
            DoubleAnimation a = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320));
            a.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(OpacityProperty, a);
        }

        private void Note(string t)
        {
            _status.Text = t;
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + t + "\r\n");
            _log.ScrollToEnd();
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
                            _bar.Value = total > 0 ? (got * 100.0 / total) : 50;
                            _status.Text = "正在下载 " + (got / 1024) + " KB" + (total > 0 ? " / " + (total / 1024) + " KB" : "");
                        }));
                    }, delegate(string s) { Dispatcher.BeginInvoke(new Action(delegate { Note(s); })); });

                    Dispatcher.BeginInvoke(new Action(delegate { _bar.Value = 55; Note("下载完成，正在解压安装…"); }));
                    Installer.Extract(zip, Cfg.Root(), delegate(string s) { Dispatcher.BeginInvoke(new Action(delegate { Note(s); })); });
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _bar.Value = 85;
                        Installer.Shortcuts();
                        Note("桌面快捷方式已创建");
                        _bar.Value = 100;
                        RefreshStates();
                        _status.Text = "安装完成，可关闭本窗口并从桌面启动";
                        _main.Content = "重新下载";
                        _main.IsEnabled = true;
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        Note("下载失败：" + ex.Message);
                        _status.Text = "网络不通，可直接把本文件夹里的 exe 拖到 E 盘（会自动生成快捷方式）";
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
                Trace.Write("--auto 开始，安装目录 " + Cfg.Root());
                string zip = Installer.FetchZip(null, delegate(string s) { Trace.Write(s); });
                Installer.Extract(zip, Cfg.Root(), delegate(string s) { Trace.Write(s); });
                Installer.Shortcuts();
                Trace.Write("OK " + Cfg.Root());
                return;
            }
            Application app = new Application();
            app.Run(new Win());
        }
    }
}
