using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Rap.Studio
{
    public enum BtnKind { Primary, Secondary, Ghost, Danger }

    /// <summary>矢量图标（不依赖字体图标，避免缺字形）。</summary>
    public static class Icons
    {
        private static Geometry P(string data)
        {
            return Geometry.Parse(data);
        }

        private static GeometryGroup Group(params string[] data)
        {
            GeometryGroup g = new GeometryGroup();
            g.FillRule = FillRule.Nonzero;
            foreach (string d in data) g.Children.Add(Geometry.Parse(d));
            return g;
        }

        public static Geometry Radar()
        {
            return Group("M9,2.2 A6.8,6.8 0 1 1 8.99,2.2", "M9,9 L13.4,4.6", "M9,7 A2,2 0 1 1 8.99,7");
        }

        public static Geometry Video()
        {
            return Group("M2.5,5.4 H11 V12.8 H2.5 Z", "M11,8.4 L15.5,5.8 V12.6 L11,10 Z");
        }

        public static Geometry Mic()
        {
            return Group("M9,2.6 A2,2 0 0 1 11,4.6 V9 A2,2 0 0 1 7,9 V4.6 A2,2 0 0 1 9,2.6 Z",
                         "M9,11.4 V14", "M6.4,14 H11.6");
        }

        public static Geometry Lyrics()
        {
            return Group("M3,3.4 H15 V11.2 H8.9 L5.3,13.9 V11.2 H3 Z",
                         "M5.8,6.4 H12.2", "M5.8,8.8 H10.4");
        }

        public static Geometry Sliders()
        {
            return Group("M2.4,5.6 H15.6", "M2.4,12.4 H15.6",
                         "M6.2,4 A1.6,1.6 0 1 1 6.19,4", "M12.2,10.8 A1.6,1.6 0 1 1 12.19,10.8");
        }

        public static Geometry Info()
        {
            return Group("M9,2.4 A6.6,6.6 0 1 1 8.99,2.4", "M9,8.4 V12.4", "M9,5.6 A0.9,0.9 0 1 1 8.99,5.6");
        }

        public static Geometry Play()
        {
            return Group("M5,3.4 L14,9 L5,14.6 Z");
        }

        public static Geometry Stop()
        {
            return Group("M5.4,5.4 H12.6 V12.6 H5.4 Z");
        }

        public static Geometry Folder()
        {
            return Group("M2.4,5.6 H7.6 L9.2,7.6 H15.6 V13.6 H2.4 Z");
        }

        public static Geometry Record()
        {
            return Group("M9,3.6 A5.4,5.4 0 1 1 8.99,3.6", "M9,6.6 A2.4,2.4 0 1 1 8.99,6.6");
        }

        public static Geometry Close()
        {
            return Group("M4.4,4.4 L13.6,13.6", "M13.6,4.4 L4.4,13.6");
        }

        public static Geometry Min()
        {
            return Group("M4,9 H14");
        }

        public static Geometry Max()
        {
            return Group("M4.6,4.6 H13.4 V13.4 H4.6 Z");
        }

        public static Geometry Check()
        {
            return Group("M3.6,9.4 L7.4,13.2 L14.4,5.2");
        }

        public static Geometry Trash()
        {
            return Group("M4,6.4 H14", "M6.6,6.4 V14.4 H11.4 V6.4", "M7.4,6.4 V4.4 H10.6 V6.4");
        }

        public static Path Glyph(Geometry g, Color color, double size, bool filled)
        {
            Path p = new Path();
            p.Data = g;
            p.Width = size;
            p.Height = size;
            p.Stretch = Stretch.Uniform;
            p.VerticalAlignment = VerticalAlignment.Center;
            p.HorizontalAlignment = HorizontalAlignment.Center;
            if (filled)
            {
                p.Fill = new SolidColorBrush(color);
            }
            else
            {
                p.Stroke = new SolidColorBrush(color);
                p.StrokeThickness = 1.5;
                p.StrokeLineJoin = PenLineJoin.Round;
            }
            return p;
        }
    }

    /// <summary>常用排版与绘制助手。</summary>
    public static class UI
    {
        public static TextBlock T(string text, double size, Brush color)
        {
            return T(text, size, color, Theme.Normal);
        }

        public static TextBlock T(string text, double size, Brush color, FontWeight weight)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontFamily = Theme.UiFont;
            t.FontSize = size;
            t.Foreground = color;
            t.FontWeight = weight;
            t.TextWrapping = TextWrapping.Wrap;
            return t;
        }

        public static Border Card(double padding)
        {
            Border b = new Border();
            b.Background = Theme.Card;
            b.CornerRadius = new CornerRadius(10);
            b.BorderBrush = Theme.Border;
            b.BorderThickness = new Thickness(1);
            b.Padding = new Thickness(padding);
            return b;
        }

        public static Border Pill(string text, Brush fg, Brush bg)
        {
            TextBlock t = T(text, 11, fg, Theme.Bold);
            Border b = new Border();
            b.Background = bg;
            b.CornerRadius = new CornerRadius(9);
            b.Padding = new Thickness(8, 2, 8, 2);
            b.VerticalAlignment = VerticalAlignment.Center;
            b.Child = t;
            return b;
        }

        public static Rectangle Divider()
        {
            Rectangle r = new Rectangle();
            r.Height = 1;
            r.Fill = Theme.Border;
            r.Margin = new Thickness(0, 12, 0, 12);
            return r;
        }

        public static StackPanel V(double gap)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Vertical;
            if (gap > 0) { } // 间距由调用方用 Margin 控制
            return sp;
        }

        public static StackPanel H(double gap)
        {
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            return sp;
        }

        public static void AnimateDouble(DependencyObject target, DependencyProperty dp, double to, int ms, IEasingFunction ease)
        {
            DoubleAnimation a = new DoubleAnimation();
            a.To = to;
            a.Duration = new Duration(TimeSpan.FromMilliseconds(ms));
            if (ease != null) a.EasingFunction = ease;
            if (target is UIElement) ((UIElement)target).BeginAnimation(dp, a);
            else ((Animatable)target).BeginAnimation(dp, a);
        }

        public static void AnimateColor(SolidColorBrush brush, Color to, int ms)
        {
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Theme.EaseOut });
        }

        /// <summary>淡入 + 轻微上浮，用于卡片/页面入场。</summary>
        public static void Enter(UIElement el, int delayMs)
        {
            el.Opacity = 0;
            TranslateTransform tr = new TranslateTransform(0, 10);
            el.RenderTransform = tr;
            DoubleAnimation fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(320));
            fade.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            fade.EasingFunction = Theme.EaseOut;
            el.BeginAnimation(UIElement.OpacityProperty, fade);
            DoubleAnimation slide = new DoubleAnimation(0, TimeSpan.FromMilliseconds(360));
            slide.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            slide.EasingFunction = Theme.EaseOut;
            tr.BeginAnimation(TranslateTransform.YProperty, slide);
        }
    }

    /// <summary>圆角按钮，带悬停/按下动画。</summary>
    public class FlatButton : Grid
    {
        private Border _bg;
        private TextBlock _label;
        private Path _icon;
        private BtnKind _kind;
        private bool _enabled = true;
        private bool _hover;
        private bool _press;
        private ScaleTransform _scale;

        public event RoutedEventHandler Click;

        public FlatButton(string text, BtnKind kind)
        {
            _kind = kind;
            Background = Brushes.Transparent;
            Cursor = Cursors.Hand;
            MinHeight = 36;
            SnapsToDevicePixels = true;
            _scale = new ScaleTransform(1, 1);
            RenderTransform = _scale;
            RenderTransformOrigin = new Point(0.5, 0.5);

            _bg = new Border();
            _bg.CornerRadius = new CornerRadius(8);
            _bg.Background = BaseBrush();
            Children.Add(_bg);

            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            sp.VerticalAlignment = VerticalAlignment.Center;
            sp.Margin = new Thickness(16, 0, 16, 0);

            _icon = new Path();
            _icon.Width = 15;
            _icon.Height = 15;
            _icon.Stretch = Stretch.Uniform;
            _icon.Margin = new Thickness(0, 0, 7, 0);
            _icon.Visibility = Visibility.Collapsed;
            _icon.VerticalAlignment = VerticalAlignment.Center;
            _icon.Fill = new SolidColorBrush(LabelColor());
            sp.Children.Add(_icon);

            _label = UI.T(text, 13, new SolidColorBrush(LabelColor()), Theme.Bold);
            _label.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(_label);
            Children.Add(sp);

            MouseEnter += OnEnter;
            MouseLeave += OnLeave;
            MouseLeftButtonDown += OnDown;
            MouseLeftButtonUp += OnUp;
        }

        public string Text
        {
            get { return _label.Text; }
            set { _label.Text = value; }
        }

        public void SetIcon(Geometry g, bool filled)
        {
            _icon.Data = g;
            if (filled) { _icon.Fill = new SolidColorBrush(LabelColor()); _icon.Stroke = null; }
            else { _icon.Stroke = new SolidColorBrush(LabelColor()); _icon.Fill = null; _icon.StrokeThickness = 1.5; }
            _icon.Visibility = Visibility.Visible;
        }

        public void SetEnabled(bool on)
        {
            _enabled = on;
            Opacity = on ? 1.0 : 0.45;
            Cursor = on ? Cursors.Hand : Cursors.Arrow;
        }

        private Color LabelColor()
        {
            switch (_kind)
            {
                case BtnKind.Primary: return Colors.White;
                case BtnKind.Danger: return Theme.DangerColor;
                case BtnKind.Ghost: return Theme.SubColor;
                default: return Theme.TextColor;
            }
        }

        private Color BaseColor()
        {
            switch (_kind)
            {
                case BtnKind.Primary: return Theme.AccentColor;
                case BtnKind.Danger: return Color.FromRgb(0xFE, 0xEC, 0xEC);
                case BtnKind.Ghost: return Color.FromArgb(0, 255, 255, 255);
                default: return Color.FromRgb(0xF5, 0xF6, 0xF8);
            }
        }

        private Color HoverColor()
        {
            switch (_kind)
            {
                case BtnKind.Primary: return Theme.AccentDeepColor;
                case BtnKind.Danger: return Color.FromRgb(0xFC, 0xD9, 0xD9);
                case BtnKind.Ghost: return Color.FromRgb(0xF5, 0xF6, 0xF8);
                default: return Color.FromRgb(0xEB, 0xEE, 0xF2);
            }
        }

        private SolidColorBrush BaseBrush()
        {
            return new SolidColorBrush(_hover ? HoverColor() : BaseColor());
        }

        private void OnEnter(object s, MouseEventArgs e)
        {
            if (!_enabled) return;
            _hover = true;
            AnimBrush(HoverColor(), 140);
        }

        private void OnLeave(object s, MouseEventArgs e)
        {
            _hover = false;
            _press = false;
            AnimBrush(BaseColor(), 180);
            UI.AnimateDouble(_scale, ScaleTransform.ScaleXProperty, 1.0, 180, Theme.EaseOut);
            UI.AnimateDouble(_scale, ScaleTransform.ScaleYProperty, 1.0, 180, Theme.EaseOut);
        }

        private void OnDown(object s, MouseButtonEventArgs e)
        {
            if (!_enabled) return;
            _press = true;
            UI.AnimateDouble(_scale, ScaleTransform.ScaleXProperty, 0.965, 90, Theme.EaseOut);
            UI.AnimateDouble(_scale, ScaleTransform.ScaleYProperty, 0.965, 90, Theme.EaseOut);
        }

        private void OnUp(object s, MouseButtonEventArgs e)
        {
            if (!_enabled) return;
            UI.AnimateDouble(_scale, ScaleTransform.ScaleXProperty, 1.0, 200, Theme.EaseOut);
            UI.AnimateDouble(_scale, ScaleTransform.ScaleYProperty, 1.0, 200, Theme.EaseOut);
            if (_press && Click != null) Click(this, new RoutedEventArgs());
            _press = false;
        }

        private void AnimBrush(Color to, int ms)
        {
            SolidColorBrush b = _bg.Background as SolidColorBrush;
            if (b == null || b.IsFrozen)
            {
                b = new SolidColorBrush(b == null ? to : b.Color);
                _bg.Background = b;
            }
            UI.AnimateColor(b, to, ms);
        }
    }

    /// <summary>左侧导航项，选中时带柔和底色与左侧色条。</summary>
    public class NavItem : Grid
    {
        private Border _pill;
        private Rectangle _bar;
        private Path _icon;
        private TextBlock _text;
        private bool _selected;
        private bool _hover;

        public event EventHandler Pick;

        public string Key;

        public NavItem(string key, string label, Geometry icon)
        {
            Key = key;
            Height = 42;
            Margin = new Thickness(10, 2, 10, 2);
            Background = Brushes.Transparent;
            Cursor = Cursors.Hand;

            _pill = new Border();
            _pill.CornerRadius = new CornerRadius(8);
            _pill.Background = new SolidColorBrush(Colors.Transparent);
            Children.Add(_pill);

            _bar = new Rectangle();
            _bar.Width = 3;
            _bar.Height = 16;
            _bar.RadiusX = 1.5;
            _bar.RadiusY = 1.5;
            _bar.Fill = Theme.Accent;
            _bar.HorizontalAlignment = HorizontalAlignment.Left;
            _bar.VerticalAlignment = VerticalAlignment.Center;
            _bar.Margin = new Thickness(3, 0, 0, 0);
            _bar.Opacity = 0;
            Children.Add(_bar);

            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;
            sp.Margin = new Thickness(18, 0, 0, 0);

            _icon = Icons.Glyph(icon, Theme.SubColor, 17, false);
            _icon.Margin = new Thickness(0, 0, 10, 0);
            _icon.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(_icon);

            _text = UI.T(label, 13, Theme.Sub, Theme.Normal);
            _text.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(_text);
            Children.Add(sp);

            MouseEnter += OnEnter;
            MouseLeave += OnLeave;
            MouseLeftButtonUp += OnUp;
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Apply();
            }
        }

        private void Apply()
        {
            Color pillTo = _selected ? Theme.AccentSoftColor : (_hover ? Color.FromRgb(0xF5, 0xF6, 0xF8) : Colors.Transparent);
            SolidColorBrush b = _pill.Background as SolidColorBrush;
            if (b == null || b.IsFrozen) { b = new SolidColorBrush(b == null ? Colors.Transparent : b.Color); _pill.Background = b; }
            UI.AnimateColor(b, pillTo, 200);
            _text.Foreground = _selected ? Theme.AccentDeep : (_hover ? Theme.Text : Theme.Sub);
            _text.FontWeight = _selected ? Theme.Bold : Theme.Normal;
            _icon.Stroke = new SolidColorBrush(_selected ? Theme.AccentColor : (_hover ? Theme.TextColor : Theme.SubColor));
            _icon.StrokeThickness = _selected ? 1.8 : 1.5;
            UI.AnimateDouble(_bar, OpacityProperty, _selected ? 1 : 0, 180, Theme.EaseOut);
        }

        private void OnEnter(object s, MouseEventArgs e)
        {
            _hover = true;
            Apply();
        }

        private void OnLeave(object s, MouseEventArgs e)
        {
            _hover = false;
            Apply();
        }

        private void OnUp(object s, MouseButtonEventArgs e)
        {
            if (Pick != null) Pick(this, EventArgs.Empty);
        }
    }

    /// <summary>电平条：用两列星号宽度实现，无需重绘。</summary>
    public class LevelMeter : Grid
    {
        private ColumnDefinition _fill;
        private ColumnDefinition _rest;
        private double _level;
        private bool _danger;

        public LevelMeter(double height)
        {
            Height = height;
            Background = Brushes.Transparent;
            ClipToBounds = true;

            Border track = new Border();
            track.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xF1, 0xF5));
            track.CornerRadius = new CornerRadius(height / 2);
            Grid.SetColumnSpan(track, 2);
            Children.Add(track);

            _fill = new ColumnDefinition();
            _fill.Width = new GridLength(0, GridUnitType.Pixel);
            _rest = new ColumnDefinition();
            _rest.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinitions.Add(_fill);
            ColumnDefinitions.Add(_rest);

            Border bar = new Border();
            bar.Background = new LinearGradientBrush(Color.FromRgb(0xFF, 0x8A, 0x5C), Theme.AccentColor, 0);
            bar.CornerRadius = new CornerRadius(height / 2);
            Grid.SetColumn(bar, 0);
            Children.Add(bar);
        }

        public void SetLevel(double v, bool live)
        {
            _level = v;
            _danger = live;
            double w = ActualWidth * Math.Min(1.0, Math.Max(0.0, v));
            if (double.IsNaN(w) || w <= 0 || ActualWidth <= 0) return;
            _fill.Width = new GridLength(w, GridUnitType.Pixel);
            _rest.Width = new GridLength(Math.Max(0.0, ActualWidth - w), GridUnitType.Pixel);
        }
    }

    /// <summary>右上角浮层提示。</summary>
    public static class Toast
    {
        private static int _slot;

        public static void Show(Window owner, string text, bool good)
        {
            if (owner == null) return;
            Window w = new Window();
            w.WindowStyle = WindowStyle.None;
            w.AllowsTransparency = true;
            w.Background = Brushes.Transparent;
            w.ShowInTaskbar = false;
            w.Topmost = true;
            w.Owner = owner;
            w.SizeToContent = SizeToContent.WidthAndHeight;
            w.ShowActivated = false;

            Border b = new Border();
            b.CornerRadius = new CornerRadius(9);
            b.Background = new SolidColorBrush(good ? Color.FromArgb(0xF2, 0x1B, 0x2A, 0x24) : Color.FromArgb(0xF2, 0x2A, 0x1D, 0x1B));
            b.Padding = new Thickness(14, 9, 16, 9);

            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            Path p = Icons.Glyph(good ? Icons.Check() : Icons.Info(), good ? Color.FromRgb(0x5A, 0xE0, 0x9C) : Color.FromRgb(0xFF, 0xB0, 0x8F), 15, false);
            p.Margin = new Thickness(0, 0, 9, 0);
            sp.Children.Add(p);
            TextBlock t = UI.T(text, 12.5, new SolidColorBrush(Colors.White), Theme.Normal);
            t.MaxWidth = 420;
            sp.Children.Add(t);
            b.Child = sp;
            w.Content = b;

            w.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double hh = w.DesiredSize.Height;
            int slot = _slot % 3;
            _slot++;
            w.Left = owner.Left + (owner.ActualWidth - w.DesiredSize.Width) / 2;
            w.Top = owner.Top + 62 + slot * (hh + 8);

            w.Opacity = 0;
            TranslateTransform tr = new TranslateTransform(0, -8);
            b.RenderTransform = tr;
            w.Show();
            DoubleAnimation fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = Theme.EaseOut };
            w.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            DoubleAnimation slide = new DoubleAnimation(0, TimeSpan.FromMilliseconds(320)) { EasingFunction = Theme.EaseOut };
            tr.BeginAnimation(TranslateTransform.YProperty, slide);

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(2600);
            timer.Tick += delegate
            {
                timer.Stop();
                DoubleAnimation fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(240));
                fadeOut.Completed += delegate { try { w.Close(); } catch { } };
                w.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            timer.Start();
        }
    }
}
