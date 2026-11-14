using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Shell;
using Rap.Studio.Engine;

namespace Rap.Studio
{
    /// <summary>所有页面的基类：统一的标题/卡片样式。</summary>
    public class PageBase : Grid
    {
        protected AppHost Host;

        public PageBase(AppHost host)
        {
            Host = host;
            Background = Brushes.Transparent;
        }

        protected Config Cfg { get { return Host.Cfg; } }
        protected LogBus Log { get { return Host.Log; } }

        protected TextBlock H2(string text)
        {
            TextBlock t = UI.T(text, 12.5, Theme.Muted(), Theme.Bold);
            t.Margin = new Thickness(0, 0, 0, 8);
            return t;
        }

        protected TextBlock Body(string text)
        {
            TextBlock t = UI.T(text, 12.5, Theme.Sub, Theme.Normal);
            t.LineHeight = 19;
            return t;
        }

        protected Border Card(UIElement child, double pad)
        {
            Border b = UI.Card(pad);
            b.Child = child;
            b.Margin = new Thickness(0, 0, 0, 12);
            return b;
        }

        protected Border Row(string label, UIElement right)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 6, 0, 6);
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(118);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            TextBlock t = UI.T(label, 12.5, Theme.Sub, Theme.Normal);
            t.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(t, 0);
            g.Children.Add(t);

            if (right is FrameworkElement)
            {
                FrameworkElement fe = (FrameworkElement)right;
                fe.HorizontalAlignment = HorizontalAlignment.Left;
                fe.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(fe, 1);
                g.Children.Add(fe);
            }

            Border wrap = new Border();
            wrap.Child = g;
            return wrap;
        }

        public virtual void OnShown() { }
    }

    public class MainWindow : Window
    {
        public AppHost Host;

        private Grid _content;
        private readonly Dictionary<string, NavItem> _navItems = new Dictionary<string, NavItem>();
        private readonly Dictionary<string, FrameworkElement> _pages = new Dictionary<string, FrameworkElement>();
        private string _current = "";
        private TextBlock _statusLabel;
        private Ellipse _statusDot;
        private Border _navRail;

        public PageHome Home;
        public PageRecord Record;
        public PageLyric Lyric;
        public PageSettings Settings;
        public PageAbout About;

        public MainWindow(AppHost host)
        {
            Host = host;
            Title = AppInfo.DisplayName + " " + AppInfo.ShortVersion;
            Width = 1180;
            Height = 730;
            MinWidth = 1040;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Theme.Bg;
            FontFamily = Theme.UiFont;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            SnapsToDevicePixels = true;

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 48;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            BuildUi();
            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                Native.TryRoundCorners(hwnd);
            }
            catch { }
            ShowPage("home");
            Host.Log.Info("界面就绪 · " + AppInfo.VersionText);

            Rap.Studio.Engine.BrowserWindow bw = BrowserWatcher.Find();
            if (bw.Found) Host.Log.Info("已识别浏览器窗口：" + bw.Platform + " · " + Shorten(bw.Title, 40));
            else Host.Log.Info("暂未发现浏览器窗口，打开 B 站等视频页面后即可自动识别");
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try { Host.Session.Stop(); } catch { }
            try { Host.Session.ShutdownLyric(); } catch { }
            try { Host.Cfg.Save(); } catch { }
        }

        private void BuildUi()
        {
            Grid root = new Grid();
            root.Background = Theme.Bg;
            RowDefinition r0 = new RowDefinition();
            r0.Height = new GridLength(48);
            RowDefinition r1 = new RowDefinition();
            r1.Height = new GridLength(1, GridUnitType.Star);
            root.RowDefinitions.Add(r0);
            root.RowDefinitions.Add(r1);

            root.Children.Add(BuildTitleBar());

            Grid body = new Grid();
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(196);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            body.ColumnDefinitions.Add(c0);
            body.ColumnDefinitions.Add(c1);

            _navRail = new Border();
            _navRail.Background = Theme.Nav;
            _navRail.BorderBrush = Theme.Border;
            _navRail.BorderThickness = new Thickness(0, 1, 1, 0);
            _navRail.Child = BuildNav();
            Grid.SetColumn(_navRail, 0);
            body.Children.Add(_navRail);

            _content = new Grid();
            _content.Margin = new Thickness(0);
            _content.ClipToBounds = true;
            Grid.SetColumn(_content, 1);
            body.Children.Add(_content);

            Grid.SetRow(body, 1);
            root.Children.Add(body);
            Content = root;
        }

        private UIElement BuildTitleBar()
        {
            Border bar = new Border();
            bar.Background = Theme.Nav;
            bar.BorderBrush = Theme.Border;
            bar.BorderThickness = new Thickness(0, 0, 0, 1);
            Grid.SetRow(bar, 0);

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = GridLength.Auto;
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            StackPanel brand = new StackPanel();
            brand.Orientation = Orientation.Horizontal;
            brand.VerticalAlignment = VerticalAlignment.Center;
            brand.Margin = new Thickness(16, 0, 0, 0);

            Path logo = null;
            Grid logoBox = new Grid();
            logoBox.Children.Add(IconDraw.Logo(22));
            logoBox.Width = 22;
            logoBox.Height = 22;
            logoBox.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(logoBox);

            TextBlock name = UI.T(AppInfo.DisplayName, 14.5, Theme.Text, Theme.Heavy);
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Margin = new Thickness(8, 0, 0, 0);
            brand.Children.Add(name);

            Border chip = UI.Pill(AppInfo.ShortVersion, Theme.AccentDeep, Theme.AccentSoft);
            chip.Margin = new Thickness(10, 1, 0, 0);
            chip.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(chip);

            Border chips = UI.Pill(AppInfo.Edition, Theme.Sub, new SolidColorBrush(Color.FromRgb(0xF4, 0xF5, 0xF7)));
            chips.Margin = new Thickness(6, 1, 0, 0);
            chips.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(chips);

            TextBlock sno = UI.T("说唱 · 检测 · 录制 · 台词", 11.5, Theme.Muted(), Theme.Normal);
            sno.VerticalAlignment = VerticalAlignment.Center;
            sno.Margin = new Thickness(14, 0, 0, 0);
            brand.Children.Add(sno);

            g.Children.Add(brand);

            StackPanel wins = new StackPanel();
            wins.Orientation = Orientation.Horizontal;
            wins.HorizontalAlignment = HorizontalAlignment.Right;
            wins.VerticalAlignment = VerticalAlignment.Top;
            wins.Children.Add(WinButton(Icons.Min(), false, delegate { WindowState = WindowState.Minimized; }));
            wins.Children.Add(WinButton(Icons.Max(), false, delegate
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }));
            wins.Children.Add(WinButton(Icons.Close(), true, delegate { Close(); }));
            Grid.SetColumn(wins, 1);
            g.Children.Add(wins);

            bar.Child = g;
            return bar;
        }

        private UIElement WinButton(Geometry glyph, bool close, Action onClick)
        {
            Border b = new Border();
            b.Width = 46;
            b.Height = 34;
            b.Cursor = Cursors.Hand;
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);

            Path p = Icons.Glyph(glyph, Theme.SubColor, 12, false);
            p.StrokeThickness = 1.4;
            b.Child = p;

            SolidColorBrush normal = new SolidColorBrush(Colors.Transparent);
            b.Background = normal;
            SolidColorBrush hover = new SolidColorBrush(close ? Color.FromRgb(0xE5, 0x3E, 0x3E) : Color.FromRgb(0xEC, 0xEE, 0xF2));

            b.MouseEnter += delegate
            {
                SolidColorBrush cur = b.Background as SolidColorBrush;
                if (cur.IsFrozen) { cur = new SolidColorBrush(cur.Color); b.Background = cur; }
                UI.AnimateColor(cur, hover.Color, 120);
                p.Stroke = new SolidColorBrush(close ? Colors.White : Theme.TextColor);
            };
            b.MouseLeave += delegate
            {
                SolidColorBrush cur = b.Background as SolidColorBrush;
                if (cur.IsFrozen) { cur = new SolidColorBrush(cur.Color); b.Background = cur; }
                UI.AnimateColor(cur, Colors.Transparent, 140);
                p.Stroke = new SolidColorBrush(Theme.SubColor);
            };
            b.MouseLeftButtonUp += delegate { onClick(); };
            return b;
        }

        private UIElement BuildNav()
        {
            Grid g = new Grid();
            RowDefinition r0 = new RowDefinition();
            r0.Height = new GridLength(1, GridUnitType.Star);
            RowDefinition r1 = new RowDefinition();
            r1.Height = GridLength.Auto;
            g.RowDefinitions.Add(r0);
            g.RowDefinitions.Add(r1);

            StackPanel list = new StackPanel();
            list.Margin = new Thickness(0, 12, 0, 0);
            AddNav(list, "home", "检测台", Icons.Radar());
            AddNav(list, "record", "录制", Icons.Video());
            AddNav(list, "lyric", "台词", Icons.Lyrics());
            if (AppInfo.IsPro) AddNav(list, "crawler", "爬虫", Icons.Radar());
            AddNav(list, "settings", "设置", Icons.Sliders());
            AddNav(list, "about", "关于", Icons.Info());
            g.Children.Add(list);

            Border foot = new Border();
            foot.Padding = new Thickness(18, 12, 14, 14);
            foot.BorderBrush = Theme.Border;
            foot.BorderThickness = new Thickness(0, 1, 0, 0);
            StackPanel fsp = new StackPanel();
            fsp.Orientation = Orientation.Vertical;

            StackPanel st = new StackPanel();
            st.Orientation = Orientation.Horizontal;
            _statusDot = new Ellipse();
            _statusDot.Width = 8;
            _statusDot.Height = 8;
            _statusDot.Fill = new SolidColorBrush(Color.FromRgb(0xB6, 0xBD, 0xC7));
            _statusDot.VerticalAlignment = VerticalAlignment.Center;
            st.Children.Add(_statusDot);

            _statusLabel = UI.T("待机", 11.5, Theme.Muted(), Theme.Normal);
            _statusLabel.Margin = new Thickness(7, 0, 0, 0);
            st.Children.Add(_statusLabel);
            fsp.Children.Add(st);

            TextBlock ver = UI.T(AppInfo.ShortVersion + " · " + AppInfo.Edition, 10.5, Theme.Muted(), Theme.Normal);
            ver.Margin = new Thickness(0, 6, 0, 0);
            fsp.Children.Add(ver);

            foot.Child = fsp;
            Grid.SetRow(foot, 1);
            g.Children.Add(foot);
            return g;
        }

        private void AddNav(StackPanel host, string key, string label, Geometry icon)
        {
            NavItem item = new NavItem(key, label, icon);
            item.Pick += delegate { ShowPage(key); };
            _navItems[key] = item;
            host.Children.Add(item);
        }

        public void ShowPage(string key)
        {
            if (_current == key) return;
            _current = key;
            foreach (KeyValuePair<string, NavItem> kv in _navItems) kv.Value.Selected = (kv.Key == key);

            FrameworkElement page = GetPage(key);
            _content.Children.Clear();
            _content.Children.Add(page);

            page.Opacity = 0;
            TranslateTransform tt = new TranslateTransform(16, 0);
            page.RenderTransform = tt;

            DoubleAnimation fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(280));
            fade.EasingFunction = Theme.EaseOut;
            page.BeginAnimation(OpacityProperty, fade);

            DoubleAnimation slide = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            slide.EasingFunction = Theme.EaseOut;
            tt.BeginAnimation(TranslateTransform.XProperty, slide);

            PageBase pb = page as PageBase;
            if (pb != null) pb.OnShown();
        }

        private FrameworkElement GetPage(string key)
        {
            FrameworkElement page;
            if (_pages.TryGetValue(key, out page)) return page;

            switch (key)
            {
                case "home":
                    Home = new PageHome(Host);
                    page = Home;
                    break;
                case "record":
                    Record = new PageRecord(Host);
                    page = Record;
                    break;
                case "lyric":
                    Lyric = new PageLyric(Host);
                    page = Lyric;
                    break;
                case "crawler":
                    page = new PageCrawler(Host);
                    break;
                case "settings":
                    Settings = new PageSettings(Host);
                    page = Settings;
                    break;
                default:
                    About = new PageAbout(Host);
                    page = About;
                    break;
            }
            _pages[key] = page;
            return page;
        }

        /// <summary>底部状态指示（待机 / 检测中 / 录制中）。</summary>
        public void SetStatus(string text, Color color, bool pulse)
        {
            if (_statusLabel == null) return;
            _statusLabel.Text = text;
            SolidColorBrush b = _statusDot.Fill as SolidColorBrush;
            if (b.IsFrozen) { b = new SolidColorBrush(b.Color); _statusDot.Fill = b; }
            UI.AnimateColor(b, color, 220);

            _statusDot.BeginAnimation(OpacityProperty, null);
            if (pulse)
            {
                DoubleAnimation a = new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(900));
                a.AutoReverse = true;
                a.RepeatBehavior = RepeatBehavior.Forever;
                _statusDot.BeginAnimation(OpacityProperty, a);
            }
            else
            {
                _statusDot.Opacity = 1;
            }
        }

        public static string Shorten(string s, int max)
        {
            if (s == null) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
