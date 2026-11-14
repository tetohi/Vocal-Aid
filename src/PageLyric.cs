using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Rap.Studio.Engine;

namespace Rap.Studio
{
    public class PageLyric : PageBase
    {
        private TextBlock _current;
        private TextBlock _currentTime;
        private Border _panel;
        private LinearGradientBrush _brush;
        private GradientStop _gs0;
        private GradientStop _gs1;
        private Ellipse _themeDot;
        private TextBlock _themeLabel;
        private StackPanel _history;
        private ScrollViewer _historyScroll;
        private TextBox _manual;
        private ComboBox _regionCombo;
        private Slider _intervalSlider;
        private TextBlock _intervalLabel;
        private TextBlock _engineTag;
        private CheckBox _autoOcr;
        private CheckBox _followTheme;
        private TextBox _fixBox;
        private System.Windows.Media.Color _theme = System.Windows.Media.Color.FromRgb(0xFF, 0x4C, 0x2B);

        public PageLyric(AppHost host) : base(host)
        {
            Build();
            Wire();
            ApplyTheme(_theme, false);
        }

        private void Build()
        {
            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Padding = new Thickness(22, 18, 22, 20);
            StackPanel root = new StackPanel();
            sv.Content = root;

            // ---------- 台词大屏 ----------
            _panel = new Border();
            _panel.CornerRadius = new CornerRadius(12);
            _panel.Margin = new Thickness(0, 0, 0, 12);
            _panel.Height = 216;

            _brush = new LinearGradientBrush();
            _brush.StartPoint = new Point(0, 0);
            _brush.EndPoint = new Point(1, 1);
            _gs0 = new GradientStop(Theme.Darken(Theme.AccentColor, 0.2), 0);
            _gs1 = new GradientStop(Theme.Soften2(Theme.AccentColor), 1);
            _brush.GradientStops.Add(_gs0);
            _brush.GradientStops.Add(_gs1);
            _panel.Background = _brush;

            Grid pg = new Grid();
            pg.Margin = new Thickness(26, 20, 26, 18);

            StackPanel head = new StackPanel();
            head.Orientation = Orientation.Horizontal;
            head.VerticalAlignment = VerticalAlignment.Top;
            TextBlock tag = UI.T("实时台词", 12, new SolidColorBrush(Color.FromArgb(0xE0, 0xFF, 0xFF, 0xFF)), Theme.Bold);
            head.Children.Add(tag);
            _engineTag = UI.T("跟随视频主题色 · 台词引擎待命", 11, new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)), Theme.Normal);
            _engineTag.Margin = new Thickness(10, 1, 0, 0);
            head.Children.Add(_engineTag);
            pg.Children.Add(head);

            StackPanel center = new StackPanel();
            center.VerticalAlignment = VerticalAlignment.Center;
            center.HorizontalAlignment = HorizontalAlignment.Left;
            center.MaxWidth = 860;

            _current = UI.T("等待台词…", 27, new SolidColorBrush(Colors.White), Theme.Heavy);
            _current.TextWrapping = TextWrapping.Wrap;
            _current.LineHeight = 38;
            DropShadowEffect shadow = new DropShadowEffect();
            shadow.BlurRadius = 14;
            shadow.ShadowDepth = 2;
            shadow.Opacity = 0.28;
            shadow.Color = Colors.Black;
            _current.Effect = shadow;
            center.Children.Add(_current);
            pg.Children.Add(center);

            StackPanel foot = new StackPanel();
            foot.Orientation = Orientation.Horizontal;
            foot.VerticalAlignment = VerticalAlignment.Bottom;
            foot.HorizontalAlignment = HorizontalAlignment.Right;

            _themeDot = new Ellipse();
            _themeDot.Width = 10;
            _themeDot.Height = 10;
            _themeDot.Fill = new SolidColorBrush(Colors.White);
            _themeDot.VerticalAlignment = VerticalAlignment.Center;
            _themeDot.Opacity = 0.85;
            foot.Children.Add(_themeDot);

            _themeLabel = UI.T("主题色", 11, new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)), Theme.Normal);
            _themeLabel.Margin = new Thickness(6, 0, 14, 0);
            foot.Children.Add(_themeLabel);

            _currentTime = UI.T("--:--:--", 11, new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)), Theme.Normal);
            foot.Children.Add(_currentTime);
            pg.Children.Add(foot);

            _panel.Child = pg;
            root.Children.Add(_panel);

            // ---------- 识别设置 ----------
            Border card = UI.Card(18);
            StackPanel sp = new StackPanel();
            sp.Children.Add(H2("台词识别"));

            StackPanel row1 = new StackPanel();
            row1.Orientation = Orientation.Horizontal;
            _autoOcr = W.Check("自动识别视频字幕（Windows 本地 OCR，不联网）", Cfg.OcrEnabled);
            _autoOcr.Checked += delegate { Cfg.OcrEnabled = true; Cfg.Save(); };
            _autoOcr.Unchecked += delegate { Cfg.OcrEnabled = false; Cfg.Save(); };
            row1.Children.Add(_autoOcr);

            _followTheme = W.Check("背景跟随视频主题色", Cfg.ThemeFollow);
            _followTheme.Margin = new Thickness(22, 4, 0, 4);
            _followTheme.Checked += delegate { Cfg.ThemeFollow = true; Cfg.Save(); };
            _followTheme.Unchecked += delegate { Cfg.ThemeFollow = false; Cfg.Save(); };
            row1.Children.Add(_followTheme);
            sp.Children.Add(row1);

            List<object> regions = new List<object>();
            regions.Add("画面下方字幕带（推荐）");
            regions.Add("画面中部字幕带");
            _regionCombo = W.Combo(regions, Cfg.OcrRegion);
            _regionCombo.Width = 210;
            _regionCombo.SelectionChanged += delegate
            {
                Cfg.OcrRegion = _regionCombo.SelectedIndex < 0 ? 0 : _regionCombo.SelectedIndex;
                Cfg.Save();
            };
            sp.Children.Add(W.Inline(Label("识别区域"), _regionCombo, 84));

            StackPanel row3 = new StackPanel();
            row3.Orientation = Orientation.Horizontal;
            _intervalSlider = W.Slide(400, 3000, Cfg.OcrIntervalMs, 190);
            _intervalLabel = UI.T(Cfg.OcrIntervalMs + " ms", 12, Theme.Text, Theme.Bold);
            _intervalLabel.Width = 70;
            _intervalLabel.Margin = new Thickness(10, 0, 0, 0);
            _intervalLabel.VerticalAlignment = VerticalAlignment.Center;
            _intervalSlider.ValueChanged += delegate
            {
                Cfg.OcrIntervalMs = (int)_intervalSlider.Value;
                _intervalLabel.Text = Cfg.OcrIntervalMs + " ms";
                Cfg.Save();
            };
            row3.Children.Add(_intervalSlider);
            row3.Children.Add(_intervalLabel);
            sp.Children.Add(W.Inline(Label("识别间隔"), row3, 84));

            StackPanel row4 = new StackPanel();
            row4.Orientation = Orientation.Horizontal;
            _manual = W.Text("", 420);
            _manual.KeyDown += delegate(object s, System.Windows.Input.KeyEventArgs e)
            {
                if (e.Key == System.Windows.Input.Key.Enter) SendManual();
            };
            row4.Children.Add(_manual);

            FlatButton send = new FlatButton("发送台词", BtnKind.Primary);
            send.Height = 30;
            send.MinHeight = 30;
            send.Margin = new Thickness(10, 0, 0, 0);
            send.Click += delegate { SendManual(); };
            row4.Children.Add(send);

            FlatButton import = new FlatButton("导入歌词文本", BtnKind.Secondary);
            import.Height = 30;
            import.MinHeight = 30;
            import.Margin = new Thickness(8, 0, 0, 0);
            import.Click += delegate { ImportLyrics(); };
            row4.Children.Add(import);
            sp.Children.Add(W.Inline(Label("手动录入"), row4, 84));

            if (AppInfo.IsPro)
            {
                StackPanel row5 = new StackPanel();
                row5.Orientation = Orientation.Horizontal;
                _fixBox = W.Text(string.Join(" ", Cfg.OcrFix.ToArray()), 420);
                _fixBox.ToolTip = "格式：错字=>正字，多个用空格分隔";
                _fixBox.TextChanged += delegate
                {
                    Cfg.OcrFix.Clear();
                    string[] parts = _fixBox.Text.Split(new char[] { ' ', ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string p in parts) if (p.Contains("=>")) Cfg.OcrFix.Add(p);
                    Cfg.Save();
                };
                row5.Children.Add(_fixBox);
                TextBlock hint = UI.T("纠错词典，例：说畅=>说唱", 11, Theme.Muted(), Theme.Normal);
                hint.Margin = new Thickness(10, 0, 0, 0);
                hint.VerticalAlignment = VerticalAlignment.Center;
                row5.Children.Add(hint);
                sp.Children.Add(W.Inline(Label("纠错词典"), row5, 84));
            }

            card.Child = sp;
            root.Children.Add(card);

            // ---------- 台词历史 ----------
            Border hisCard = UI.Card(18);
            Grid hg = new Grid();
            RowDefinition r0 = new RowDefinition();
            r0.Height = GridLength.Auto;
            RowDefinition r1 = new RowDefinition();
            r1.Height = new GridLength(1, GridUnitType.Star);
            hg.RowDefinitions.Add(r0);
            hg.RowDefinitions.Add(r1);

            Grid head2 = new Grid();
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = GridLength.Auto;
            head2.ColumnDefinitions.Add(c0);
            head2.ColumnDefinitions.Add(c1);
            head2.Children.Add(H2("台词历史"));

            FlatButton clearBtn = new FlatButton("清空", BtnKind.Ghost);
            clearBtn.Height = 26;
            clearBtn.MinHeight = 26;
            clearBtn.Margin = new Thickness(0, 0, 0, 6);
            clearBtn.Click += delegate { _history.Children.Clear(); };
            Grid.SetColumn(clearBtn, 1);
            head2.Children.Add(clearBtn);
            Grid.SetRow(head2, 0);
            hg.Children.Add(head2);

            _historyScroll = new ScrollViewer();
            _historyScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _historyScroll.MaxHeight = 210;
            _history = new StackPanel();
            _historyScroll.Content = _history;
            Grid.SetRow(_historyScroll, 1);
            hg.Children.Add(_historyScroll);

            hisCard.Child = hg;
            root.Children.Add(hisCard);

            Children.Add(sv);
        }

        private TextBlock Label(string text)
        {
            TextBlock t = UI.T(text, 12.5, Theme.Sub, Theme.Normal);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Margin = new Thickness(0, 0, 10, 0);
            return t;
        }

        private void Wire()
        {
            Host.Session.OnLyric += delegate(LyricLine line)
            {
                AddLine(line);
            };
            Host.Session.OnThemeColor += delegate(System.Windows.Media.Color c)
            {
                _theme = c;
                ApplyTheme(c, Cfg.ThemeFollow);
            };
            Host.Session.OnOcrStatus += delegate(string msg)
            {
                _engineTag.Text = "跟随视频主题色 · " + msg;
            };
            Host.Session.OnState += delegate(SessionState st, string msg)
            {
                if (st == SessionState.Arming || st == SessionState.Idle)
                {
                    // 保持上一句台词，仅刷新状态提示
                }
            };
        }

        private void SendManual()
        {
            string text = _manual.Text.Trim();
            if (text.Length == 0) return;
            LyricLine line = new LyricLine();
            line.Text = text;
            line.Time = DateTime.Now;
            line.Color = _theme;
            AddLine(line);
            _manual.Text = "";
        }

        private void AddLine(LyricLine line)
        {
            if (line == null || string.IsNullOrEmpty(line.Text)) return;

            _current.Text = line.Text;
            _currentTime.Text = line.Time.ToString("HH:mm:ss");

            TextBlock item = UI.T(line.Text, 12.5, Theme.Text, Theme.Normal);
            item.TextWrapping = TextWrapping.Wrap;

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(60);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            TextBlock t = UI.T(line.Time.ToString("HH:mm:ss"), 11, Theme.Muted(), Theme.Normal);
            t.VerticalAlignment = VerticalAlignment.Top;
            g.Children.Add(t);

            Grid.SetColumn(item, 1);
            g.Children.Add(item);

            Border b = new Border();
            b.Child = g;
            b.Padding = new Thickness(0, 4, 0, 6);
            b.BorderBrush = Theme.Border;
            b.BorderThickness = new Thickness(0, 0, 0, 1);
            _history.Children.Add(b);
            while (_history.Children.Count > 120) _history.Children.RemoveAt(0);
            _historyScroll.ScrollToEnd();

            // 大屏做一次轻微淡入，让切词更顺滑
            _current.Opacity = 0.35;
            DoubleAnimation a = new DoubleAnimation(1, TimeSpan.FromMilliseconds(260));
            a.EasingFunction = Theme.Smooth;
            _current.BeginAnimation(UIElement.OpacityProperty, a);
        }

        private void ImportLyrics()
        {
            try
            {
                Microsoft.Win32.OpenFileDialog d = new Microsoft.Win32.OpenFileDialog();
                d.Filter = "文本文件|*.txt|所有文件|*.*";
                d.Title = "选择歌词文本";
                if (d.ShowDialog() != true) return;
                string[] lines = File.ReadAllLines(d.FileName, System.Text.Encoding.UTF8);
                foreach (string raw in lines)
                {
                    string s = raw.Trim();
                    if (s.Length == 0) continue;
                    LyricLine line = new LyricLine();
                    line.Text = s;
                    line.Time = DateTime.Now;
                    line.Color = _theme;
                    AddLine(line);
                }
                Log.Info("已导入歌词：" + System.IO.Path.GetFileName(d.FileName));
            }
            catch (Exception ex)
            {
                Log.Warn("导入歌词失败：" + ex.Message);
            }
        }

        private void ApplyTheme(System.Windows.Media.Color c, bool animate)
        {
            if (!Cfg.ThemeFollow) c = Theme.AccentColor;
            System.Windows.Media.Color top = Theme.Darken(c, 0.22);
            System.Windows.Media.Color bottom = Theme.Soften2(c);

            if (animate)
            {
                ColorAnimation a0 = new ColorAnimation(top, TimeSpan.FromMilliseconds(600));
                a0.EasingFunction = Theme.EaseOut;
                _gs0.BeginAnimation(GradientStop.ColorProperty, a0);
                ColorAnimation a1 = new ColorAnimation(bottom, TimeSpan.FromMilliseconds(600));
                a1.EasingFunction = Theme.EaseOut;
                _gs1.BeginAnimation(GradientStop.ColorProperty, a1);
            }
            else
            {
                _gs0.Color = top;
                _gs1.Color = bottom;
            }

            int lum = (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
            Brush text = lum < 150 ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Theme.TextColor);
            _current.Foreground = text;
            _themeDot.Fill = new SolidColorBrush(Theme.Lighten(c, 0.55));
            _themeLabel.Text = "主题色 #" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");

            if (Cfg.ThemeFollow)
            {
                _themeLabel.ToolTip = "背景颜色取自当前播放画面的主色";
            }
        }
    }
}
