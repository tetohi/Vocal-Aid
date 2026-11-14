using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Rap.Studio.Engine;

namespace Rap.Studio
{
    public class PageHome : PageBase
    {
        private TextBlock _stateText;
        private TextBlock _stateSub;
        private Ellipse _stateDot;
        private Border _statePlate;

        private FlatButton _startBtn;
        private FlatButton _stopBtn;
        private FlatButton _openBtn;

        private TextBlock _browserText;
        private TextBlock _pageText;
        private TextBlock _playText;
        private TextBlock _elapsedText;

        private LevelMeter _micMeter;
        private LevelMeter _sysMeter;
        private TextBlock _micVal;
        private TextBlock _sysVal;
        private TextBlock _thresholdText;

        private StackPanel _logList;
        private ScrollViewer _logScroll;
        private TextBlock _outPathText;
        private TextBlock _outMetaText;

        private Session _sn;

        public PageHome(AppHost host) : base(host)
        {
            _sn = host.Session;
            Build();
            Wire();
            RefreshIdle();
        }

        private void Build()
        {
            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Padding = new Thickness(22, 18, 22, 20);

            StackPanel root = new StackPanel();
            sv.Content = root;

            // ---------- 顶部状态卡 ----------
            Border hero = UI.Card(20);
            hero.Margin = new Thickness(0, 0, 0, 12);

            Grid hg = new Grid();
            ColumnDefinition hc0 = new ColumnDefinition();
            hc0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition hc1 = new ColumnDefinition();
            hc1.Width = GridLength.Auto;
            hg.ColumnDefinitions.Add(hc0);
            hg.ColumnDefinitions.Add(hc1);

            StackPanel left = new StackPanel();
            StackPanel statusRow = new StackPanel();
            statusRow.Orientation = Orientation.Horizontal;

            _statePlate = new Border();
            _statePlate.CornerRadius = new CornerRadius(30);
            _statePlate.Padding = new Thickness(12, 5, 14, 5);
            _statePlate.Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF3, 0xF5));
            _statePlate.VerticalAlignment = VerticalAlignment.Center;

            StackPanel inner = new StackPanel();
            inner.Orientation = Orientation.Horizontal;
            _stateDot = new Ellipse();
            _stateDot.Width = 8;
            _stateDot.Height = 8;
            _stateDot.Fill = new SolidColorBrush(Color.FromRgb(0xA9, 0xB1, 0xBC));
            _stateDot.VerticalAlignment = VerticalAlignment.Center;
            inner.Children.Add(_stateDot);
            _stateText = UI.T("待机", 13, Theme.Text, Theme.Bold);
            _stateText.Margin = new Thickness(7, 0, 0, 0);
            inner.Children.Add(_stateText);
            _statePlate.Child = inner;
            statusRow.Children.Add(_statePlate);

            left.Children.Add(statusRow);

            TextBlock big = UI.T("说唱开麦，只管唱", 24, Theme.Text, Theme.Heavy);
            big.Margin = new Thickness(0, 12, 0, 6);
            left.Children.Add(big);

            _stateSub = UI.T("打开 B 站等平台的视频页面，点击「开始检测」，软件会自动判断视频开始与结束，并同步录音录像。",
                12.5, Theme.Sub, Theme.Normal);
            _stateSub.MaxWidth = 620;
            left.Children.Add(_stateSub);

            Grid.SetColumn(left, 0);
            hg.Children.Add(left);

            StackPanel btns = new StackPanel();
            btns.Orientation = Orientation.Horizontal;
            btns.VerticalAlignment = VerticalAlignment.Center;
            btns.Margin = new Thickness(20, 0, 0, 0);

            _startBtn = new FlatButton("开始检测", BtnKind.Primary);
            _startBtn.SetIcon(Icons.Play(), true);
            _startBtn.Height = 44;
            _startBtn.Width = 158;
            _startBtn.Margin = new Thickness(0, 0, 10, 0);
            btns.Children.Add(_startBtn);

            _stopBtn = new FlatButton("停止", BtnKind.Secondary);
            _stopBtn.SetIcon(Icons.Stop(), true);
            _stopBtn.Height = 44;
            _stopBtn.Width = 108;
            _stopBtn.SetEnabled(false);
            btns.Children.Add(_stopBtn);

            Grid.SetColumn(btns, 1);
            hg.Children.Add(btns);

            hero.Child = hg;
            root.Children.Add(hero);

            // ---------- 中间两栏 ----------
            Grid mid = new Grid();
            mid.Margin = new Thickness(0, 0, 0, 12);
            ColumnDefinition mc0 = new ColumnDefinition();
            mc0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition mc1 = new ColumnDefinition();
            mc1.Width = new GridLength(1, GridUnitType.Star);
            mid.ColumnDefinitions.Add(mc0);
            mid.ColumnDefinitions.Add(mc1);

            mid.Children.Add(BuildBrowserCard());

            Border audioCard = UI.Card(18);
            audioCard.Margin = new Thickness(12, 0, 0, 0);
            StackPanel ac = new StackPanel();
            ac.Children.Add(H2("音频电平"));
            ac.Children.Add(BuildMeterRow("麦克风", true));
            ac.Children.Add(BuildMeterRow("系统声音", false));
            _thresholdText = UI.T("", 11, Theme.Muted(), Theme.Normal);
            _thresholdText.Margin = new Thickness(0, 8, 0, 0);
            ac.Children.Add(_thresholdText);
            audioCard.Child = ac;
            Grid.SetColumn(audioCard, 1);
            mid.Children.Add(audioCard);

            root.Children.Add(mid);

            // ---------- 日志 ----------
            Border logCard = UI.Card(18);
            logCard.Margin = new Thickness(0, 0, 0, 12);
            Grid lg = new Grid();
            RowDefinition lr0 = new RowDefinition();
            lr0.Height = GridLength.Auto;
            RowDefinition lr1 = new RowDefinition();
            lr1.Height = new GridLength(1, GridUnitType.Star);
            lg.RowDefinitions.Add(lr0);
            lg.RowDefinitions.Add(lr1);

            Grid lhead = new Grid();
            ColumnDefinition lh0 = new ColumnDefinition();
            lh0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition lh1 = new ColumnDefinition();
            lh1.Width = GridLength.Auto;
            lhead.ColumnDefinitions.Add(lh0);
            lhead.ColumnDefinitions.Add(lh1);
            TextBlock ltitle = UI.T("实时日志", 12.5, Theme.Muted(), Theme.Bold);
            lhead.Children.Add(ltitle);

            FlatButton clear = new FlatButton("清空", BtnKind.Ghost);
            clear.Height = 26;
            clear.MinHeight = 26;
            clear.Margin = new Thickness(0, 0, 0, 4);
            clear.Click += delegate { _logList.Children.Clear(); };
            Grid.SetColumn(clear, 1);
            lhead.Children.Add(clear);

            Grid.SetRow(lhead, 0);
            lg.Children.Add(lhead);

            _logScroll = new ScrollViewer();
            _logScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _logScroll.Height = 168;
            _logList = new StackPanel();
            _logScroll.Content = _logList;
            Grid.SetRow(_logScroll, 1);
            lg.Children.Add(_logScroll);

            logCard.Child = lg;
            root.Children.Add(logCard);

            // ---------- 最近输出 ----------
            Border outCard = UI.Card(18);
            Grid og = new Grid();
            ColumnDefinition oc0 = new ColumnDefinition();
            oc0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition oc1 = new ColumnDefinition();
            oc1.Width = GridLength.Auto;
            og.ColumnDefinitions.Add(oc0);
            og.ColumnDefinitions.Add(oc1);

            StackPanel oleft = new StackPanel();
            oleft.Children.Add(H2("最近一次输出"));
            _outPathText = UI.T("还没有录制文件。", 12.5, Theme.Text, Theme.Normal);
            oleft.Children.Add(_outPathText);
            _outMetaText = UI.T("", 11.5, Theme.Muted(), Theme.Normal);
            _outMetaText.Margin = new Thickness(0, 4, 0, 0);
            oleft.Children.Add(_outMetaText);
            og.Children.Add(oleft);

            _openBtn = new FlatButton("打开文件夹", BtnKind.Secondary);
            _openBtn.SetIcon(Icons.Folder(), false);
            _openBtn.Height = 36;
            _openBtn.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_openBtn, 1);
            og.Children.Add(_openBtn);

            outCard.Child = og;
            root.Children.Add(outCard);

            Children.Add(sv);
        }

        private Border BuildBrowserCard()
        {
            Border card = UI.Card(18);
            StackPanel sp = new StackPanel();
            sp.Children.Add(H2("浏览器状态"));
            _browserText = AddInfoRow(sp, "浏览器", "未检测");
            _pageText = AddInfoRow(sp, "当前页面", "—");
            _playText = AddInfoRow(sp, "播放状态", "—");
            _elapsedText = AddInfoRow(sp, "已录制时长", "00:00");
            card.Child = sp;
            return card;
        }

        private TextBlock AddInfoRow(StackPanel host, string label, string value)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 5, 0, 5);
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(76);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            TextBlock l = UI.T(label, 12, Theme.Sub, Theme.Normal);
            l.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(l);

            TextBlock v = UI.T(value, 12.5, Theme.Text, Theme.Normal);
            v.TextTrimming = TextTrimming.CharacterEllipsis;
            v.MaxHeight = 36;
            v.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(v, 1);
            g.Children.Add(v);

            host.Children.Add(g);
            return v;
        }

        private UIElement BuildMeterRow(string label, bool mic)
        {
            StackPanel sp = new StackPanel();
            sp.Margin = new Thickness(0, 6, 0, 6);

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = GridLength.Auto;
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            TextBlock t = UI.T(label, 12, Theme.Sub, Theme.Normal);
            g.Children.Add(t);

            TextBlock v = UI.T("0%", 11.5, Theme.Muted(), Theme.Normal);
            Grid.SetColumn(v, 1);
            g.Children.Add(v);
            sp.Children.Add(g);

            LevelMeter meter = new LevelMeter(8);
            meter.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(meter);

            if (mic) { _micMeter = meter; _micVal = v; }
            else { _sysMeter = meter; _sysVal = v; }
            return sp;
        }

        private void Wire()
        {
            _startBtn.Click += delegate
            {
                Log.Info("用户点击「开始检测」");
                _sn.Start();
                _startBtn.SetEnabled(false);
                _stopBtn.SetEnabled(true);
            };

            _stopBtn.Click += delegate
            {
                Log.Info("用户点击「停止」");
                _sn.Stop();
                _startBtn.SetEnabled(true);
                _stopBtn.SetEnabled(false);
                RefreshIdle();
            };

            _openBtn.Click += delegate
            {
                try
                {
                    Directory.CreateDirectory(Cfg.OutDir);
                    Process.Start("explorer.exe", "\"" + Cfg.OutDir + "\"");
                }
                catch (Exception ex) { Log.Warn("打开目录失败：" + ex.Message); }
            };

            _sn.OnState += delegate(SessionState st, string msg)
            {
                ApplyState(st, msg);
            };

            _sn.OnLevel += delegate(float mic, float sys)
            {
                if (_micMeter != null) { _micMeter.SetLevel(mic * 2.2, true); _micVal.Text = (int)(Math.Min(1, mic * 2.2) * 100) + "%"; }
                if (_sysMeter != null) { _sysMeter.SetLevel(sys, true); _sysVal.Text = (int)(Math.Min(1, sys) * 100) + "%"; }
            };

            _sn.OnSnapshot += delegate(BrowserWindow bw)
            {
                if (bw != null && bw.Found)
                {
                    _browserText.Text = (bw.Process.Length > 0 ? Capitalize(bw.Process) : "浏览器") + " · " + bw.Platform;
                    _pageText.Text = MainWindow.Shorten(bw.Title, 46);
                    _pageText.ToolTip = bw.Title;
                }
                else
                {
                    _browserText.Text = "未检测到浏览器窗口";
                    _pageText.Text = "请先打开 B 站、抖音等视频页面";
                }
            };

            _sn.OnRecordTime += delegate(double sec)
            {
                _elapsedText.Text = FormatTime(sec);
            };

            _sn.OnSaved += delegate(RecordResult r)
            {
                _outPathText.Text = System.IO.Path.GetFileName(r.Path);
                _outMetaText.Text = "时长 " + FormatTime(r.Seconds) + " · " + (r.Bytes / 1024 / 1024) + " MB · 延迟补偿 " + (int)r.LatencyMs + "ms";
                _elapsedText.Text = "00:00";
                if (Host.Window != null) Toast.Show(Host.Window, "录制已保存：" + System.IO.Path.GetFileName(r.Path), true);
            };

            Log.OnLine += delegate(LogEntry e)
            {
                Ui.Post(delegate { AppendLog(e); });
            };

            // 历史日志回填
            foreach (LogEntry e in Log.Items) AppendLog(e);

            _thresholdText.Text = "判定阈值：" + Cfg.SilenceThreshold.ToString("0.000") +
                "　静音判定：" + Cfg.SilenceMs + "ms　帧率：" + Cfg.Fps + "fps";
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        private void AppendLog(LogEntry e)
        {
            if (_logList == null) return;
            Color c = Theme.SubColor;
            if (e.Kind == LogKind.Good) c = Color.FromRgb(0x14, 0xA3, 0x5A);
            else if (e.Kind == LogKind.Warn) c = Color.FromRgb(0xC2, 0x7A, 0x00);
            else if (e.Kind == LogKind.Error) c = Color.FromRgb(0xE5, 0x3E, 0x3E);

            Grid g = new Grid();
            g.Margin = new Thickness(0, 0, 0, 5);
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(62);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            TextBlock t = UI.T(e.Time.ToString("HH:mm:ss"), 11, Theme.Muted(), Theme.Normal);
            g.Children.Add(t);

            TextBlock m = UI.T(e.Text, 11.5, new SolidColorBrush(c), Theme.Normal);
            m.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(m, 1);
            g.Children.Add(m);

            _logList.Children.Add(g);
            while (_logList.Children.Count > 140) _logList.Children.RemoveAt(0);
            _logScroll.ScrollToEnd();
        }

        private static string FormatTime(double sec)
        {
            if (sec < 0) sec = 0;
            int s = (int)sec;
            return string.Format("{0:00}:{1:00}", s / 60, s % 60);
        }

        private void RefreshIdle()
        {
            if (_sn.State == SessionState.Idle) ApplyState(SessionState.Idle, "");
            else ApplyState(_sn.State, "");
        }

        private void ApplyState(SessionState st, string msg)
        {
            Color dot = Color.FromRgb(0xA9, 0xB1, 0xBC);
            Color plate = Color.FromRgb(0xF2, 0xF3, 0xF5);
            Color text = Theme.TextColor;
            string label = "待机";

            switch (st)
            {
                case SessionState.Arming:
                    dot = Theme.WarnColor;
                    plate = Color.FromRgb(0xFF, 0xF6, 0xE6);
                    text = Color.FromRgb(0xA8, 0x6A, 0x00);
                    label = "正在检测";
                    break;
                case SessionState.Recording:
                    dot = Theme.AccentColor;
                    plate = Theme.AccentSoftColor;
                    text = Theme.AccentDeepColor;
                    label = "录制中";
                    break;
                case SessionState.Finalizing:
                    dot = Theme.SubColor;
                    label = "保存中";
                    break;
            }

            _stateText.Text = label;
            _stateSub.Text = string.IsNullOrEmpty(msg) ? DefaultSub(st) : msg;

            SolidColorBrush pb = _statePlate.Background as SolidColorBrush;
            if (pb.IsFrozen) { pb = new SolidColorBrush(pb.Color); _statePlate.Background = pb; }
            UI.AnimateColor(pb, plate, 260);

            SolidColorBrush db = _stateDot.Fill as SolidColorBrush;
            if (db.IsFrozen) { db = new SolidColorBrush(db.Color); _stateDot.Fill = db; }
            UI.AnimateColor(db, dot, 260);
            _stateText.Foreground = new SolidColorBrush(text);

            _stateDot.BeginAnimation(OpacityProperty, null);
            if (st == SessionState.Recording || st == SessionState.Arming)
            {
                DoubleAnimation a = new DoubleAnimation(1, 0.2, TimeSpan.FromMilliseconds(800));
                a.AutoReverse = true;
                a.RepeatBehavior = RepeatBehavior.Forever;
                _stateDot.BeginAnimation(OpacityProperty, a);
            }
            else
            {
                _stateDot.Opacity = 1;
            }

            if (st == SessionState.Recording) _playText.Text = "播放中 · 已开始录制";
            else if (st == SessionState.Arming) _playText.Text = "等待视频开始…";
            else if (st == SessionState.Idle) _playText.Text = "—";

            if (Host.Window != null)
            {
                if (st == SessionState.Recording) Host.Window.SetStatus("录制中", Theme.AccentColor, true);
                else if (st == SessionState.Arming) Host.Window.SetStatus("检测中", Theme.WarnColor, true);
                else Host.Window.SetStatus("待机", Color.FromRgb(0xB6, 0xBD, 0xC7), false);
            }
        }

        private static string DefaultSub(SessionState st)
        {
            switch (st)
            {
                case SessionState.Arming:
                    return "正在监听浏览器播放状态，检测到视频开始会自动录制。";
                case SessionState.Recording:
                    return "正在同步录制视频画面与麦克风声音，视频结束时自动停止。";
                case SessionState.Idle:
                    return "打开 B 站等平台的视频页面，点击「开始检测」，软件会自动判断视频开始与结束，并同步录音录像。";
            }
            return "";
        }
    }
}
