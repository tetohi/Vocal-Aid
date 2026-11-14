using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Rap.Studio
{
    /// <summary>首次启动的四步引导。</summary>
    public class IntroWindow : Window
    {
        private int _step;
        private Grid _stage;
        private StackPanel _dots;
        private FlatButton _prevBtn;
        private FlatButton _nextBtn;
        private TextBlock _stepText;

        private string[] _titles = new string[] {
            "欢迎使用 说唱主播助手",
            "它会自动看见这些",
            "录制前的三件小事",
            "一切就绪"
        };

        private string[] _bodies = new string[] {
            "这是一个为说唱主播准备的小工具：打开 B 站等平台的视频，它会自己判断视频什么时候开始、什么时候结束，并把画面、系统声音和你的麦克风同步录下来，同时把视频里的台词实时打到屏幕上。",
            "① 读取浏览器状态，识别当前平台与页面；② 判断视频开始与结束；③ 合成录制画面 + 原声 + 麦克风；④ 识别字幕台词，背景色跟随视频主题色。",
            "第一，录制需要麦克风权限，第一次运行时 Windows 可能会弹窗询问；第二，戴耳机时麦克风只收你的声音，用外放时原声也会被麦克收录，属于正常现象；第三，录像以 AVI（MJPEG）格式保存到「我的视频\\说唱主播助手」，可在设置里更改。",
            "点击主界面的「开始检测」，四大功能会一起进入工作状态。检测到视频开始会自动录制，视频结束会自动保存。你也可以随时在「检测台」点「停止」。"
        };

        private Geometry[] _icons = new Geometry[] {
            Icons.Radar(), Icons.Video(), Icons.Mic(), Icons.Play()
        };

        public IntroWindow(AppHost host)
        {
            Width = 660;
            Height = 486;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            FontFamily = Theme.UiFont;

            Border shell = new Border();
            shell.CornerRadius = new CornerRadius(14);
            shell.Background = Brushes.White;
            shell.BorderBrush = Theme.Border;
            shell.BorderThickness = new Thickness(1);
            DropShadowEffect sh = new DropShadowEffect();
            sh.BlurRadius = 30;
            sh.ShadowDepth = 6;
            sh.Opacity = 0.16;
            sh.Color = Colors.Black;
            shell.Effect = sh;

            Grid root = new Grid();
            root.Margin = new Thickness(4);
            RowDefinition r0 = new RowDefinition();
            r0.Height = GridLength.Auto;
            RowDefinition r1 = new RowDefinition();
            r1.Height = new GridLength(1, GridUnitType.Star);
            RowDefinition r2 = new RowDefinition();
            r2.Height = GridLength.Auto;
            root.RowDefinitions.Add(r0);
            root.RowDefinitions.Add(r1);
            root.RowDefinitions.Add(r2);

            // 顶部
            Grid top = new Grid();
            top.Margin = new Thickness(26, 18, 26, 0);
            ColumnDefinition tc0 = new ColumnDefinition();
            tc0.Width = GridLength.Auto;
            ColumnDefinition tc1 = new ColumnDefinition();
            tc1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition tc2 = new ColumnDefinition();
            tc2.Width = GridLength.Auto;
            top.ColumnDefinitions.Add(tc0);
            top.ColumnDefinitions.Add(tc1);
            top.ColumnDefinitions.Add(tc2);

            Grid logoBox = new Grid();
            logoBox.Width = 34;
            logoBox.Height = 34;
            logoBox.VerticalAlignment = VerticalAlignment.Center;
            logoBox.Children.Add(IconDraw.Logo(34));
            top.Children.Add(logoBox);

            StackPanel brandText = new StackPanel();
            brandText.Margin = new Thickness(10, 0, 0, 0);
            brandText.VerticalAlignment = VerticalAlignment.Center;
            TextBlock bn = UI.T(AppInfo.DisplayName, 14, Theme.Text, Theme.Heavy);
            brandText.Children.Add(bn);
            TextBlock bs = UI.T(AppInfo.VersionText, 10.5, Theme.Muted(), Theme.Normal);
            brandText.Children.Add(bs);
            Grid.SetColumn(brandText, 1);
            top.Children.Add(brandText);

            Border closeBtn = new Border();
            closeBtn.Width = 30;
            closeBtn.Height = 30;
            closeBtn.Cursor = System.Windows.Input.Cursors.Hand;
            closeBtn.Child = Icons.Glyph(Icons.Close(), Theme.SubColor, 11, false);
            closeBtn.MouseLeftButtonUp += delegate { Close(); };
            Grid.SetColumn(closeBtn, 2);
            top.Children.Add(closeBtn);

            Grid.SetRow(top, 0);
            root.Children.Add(top);

            // 内容
            _stage = new Grid();
            _stage.Margin = new Thickness(30, 8, 30, 0);
            Grid.SetRow(_stage, 1);
            root.Children.Add(_stage);

            // 底部
            Grid bottom = new Grid();
            bottom.Margin = new Thickness(30, 0, 26, 22);
            ColumnDefinition bc0 = new ColumnDefinition();
            bc0.Width = GridLength.Auto;
            ColumnDefinition bc1 = new ColumnDefinition();
            bc1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition bc2 = new ColumnDefinition();
            bc2.Width = GridLength.Auto;
            bottom.ColumnDefinitions.Add(bc0);
            bottom.ColumnDefinitions.Add(bc1);
            bottom.ColumnDefinitions.Add(bc2);

            _dots = new StackPanel();
            _dots.Orientation = Orientation.Horizontal;
            _dots.VerticalAlignment = VerticalAlignment.Center;
            bottom.Children.Add(_dots);

            _stepText = UI.T("", 11, Theme.Muted(), Theme.Normal);
            _stepText.HorizontalAlignment = HorizontalAlignment.Center;
            _stepText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_stepText, 1);
            bottom.Children.Add(_stepText);

            StackPanel btns = new StackPanel();
            btns.Orientation = Orientation.Horizontal;
            _prevBtn = new FlatButton("上一步", BtnKind.Ghost);
            _prevBtn.Height = 36;
            _prevBtn.Width = 92;
            _prevBtn.Margin = new Thickness(0, 0, 8, 0);
            _prevBtn.Click += delegate { Go(_step - 1); };
            btns.Children.Add(_prevBtn);

            _nextBtn = new FlatButton("下一步", BtnKind.Primary);
            _nextBtn.Height = 36;
            _nextBtn.Width = 128;
            _nextBtn.Click += delegate
            {
                if (_step >= _titles.Length - 1) Close();
                else Go(_step + 1);
            };
            btns.Children.Add(_nextBtn);
            Grid.SetColumn(btns, 2);
            bottom.Children.Add(btns);

            Grid.SetRow(bottom, 2);
            root.Children.Add(bottom);

            shell.Child = root;
            Content = shell;

            BuildDots();
            Go(0);
        }

        private void BuildDots()
        {
            _dots.Children.Clear();
            for (int i = 0; i < _titles.Length; i++)
            {
                Ellipse e = new Ellipse();
                e.Width = 7;
                e.Height = 7;
                e.Margin = new Thickness(0, 0, 6, 0);
                e.Fill = new SolidColorBrush(Color.FromRgb(0xE2, 0xE5, 0xEA));
                _dots.Children.Add(e);
            }
        }

        private void Go(int index)
        {
            if (index < 0) index = 0;
            if (index >= _titles.Length) index = _titles.Length - 1;
            _step = index;

            _stage.Children.Clear();

            StackPanel content = new StackPanel();
            content.HorizontalAlignment = HorizontalAlignment.Left;

            Border iconWrap = new Border();
            iconWrap.Width = 54;
            iconWrap.Height = 54;
            iconWrap.CornerRadius = new CornerRadius(27);
            iconWrap.Background = Theme.AccentSoft;
            iconWrap.HorizontalAlignment = HorizontalAlignment.Left;
            iconWrap.Child = Icons.Glyph(_icons[index], Theme.AccentColor, 24, false);
            content.Children.Add(iconWrap);

            TextBlock title = UI.T(_titles[index], 22, Theme.Text, Theme.Heavy);
            title.Margin = new Thickness(0, 18, 0, 10);
            content.Children.Add(title);

            TextBlock body = UI.T(_bodies[index], 13, Theme.Sub, Theme.Normal);
            body.LineHeight = 24;
            body.MaxWidth = 560;
            content.Children.Add(body);

            _stage.Children.Add(content);

            content.Opacity = 0;
            TranslateTransform tt = new TranslateTransform(0, 12);
            content.RenderTransform = tt;
            DoubleAnimation fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(320));
            fade.EasingFunction = Theme.EaseOut;
            content.BeginAnimation(UIElement.OpacityProperty, fade);
            DoubleAnimation slide = new DoubleAnimation(0, TimeSpan.FromMilliseconds(360));
            slide.EasingFunction = Theme.EaseOut;
            tt.BeginAnimation(TranslateTransform.YProperty, slide);

            for (int i = 0; i < _dots.Children.Count; i++)
            {
                Ellipse e = _dots.Children[i] as Ellipse;
                if (e == null) continue;
                SolidColorBrush b = e.Fill as SolidColorBrush;
                if (b.IsFrozen) { b = new SolidColorBrush(b.Color); e.Fill = b; }
                b.Color = (i == index) ? Theme.AccentColor : Color.FromRgb(0xE2, 0xE5, 0xEA);
            }

            _prevBtn.SetEnabled(index > 0);
            _nextBtn.Text = index >= _titles.Length - 1 ? "开始使用" : "下一步";
            _stepText.Text = (index + 1) + " / " + _titles.Length;
        }
    }
}
