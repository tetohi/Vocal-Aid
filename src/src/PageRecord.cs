using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rap.Studio.Engine;

namespace Rap.Studio
{
    public class PageRecord : PageBase
    {
        private ComboBox _micCombo;
        private ComboBox _rateCombo;
        private TextBox _latencyBox;
        private CheckBox _autoCal;
        private CheckBox _sysAudio;
        private Slider _fpsSlider;
        private TextBlock _fpsLabel;
        private ComboBox _widthCombo;
        private Slider _qualitySlider;
        private TextBlock _qualityLabel;
        private TextBox _outBox;
        private StackPanel _historyList;
        private List<MicDeviceInfo> _devices = new List<MicDeviceInfo>();

        public PageRecord(AppHost host) : base(host)
        {
            Build();
        }

        private void Build()
        {
            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Padding = new Thickness(22, 18, 22, 20);
            StackPanel root = new StackPanel();
            sv.Content = root;

            // ---------- 录音设置 ----------
            Border card = UI.Card(18);
            StackPanel sp = new StackPanel();
            sp.Children.Add(H2("录音与画面"));

            _devices = MicDevices.List();
            List<object> micItems = new List<object>();
            micItems.Add("系统默认录音设备");
            foreach (MicDeviceInfo d in _devices) micItems.Add(d);

            _micCombo = W.Combo(micItems, 0);
            for (int i = 0; i < _devices.Count; i++)
            {
                if (_devices[i].Index == Cfg.MicDevice) _micCombo.SelectedIndex = i + 1;
            }
            _micCombo.SelectionChanged += delegate
            {
                int idx = _micCombo.SelectedIndex;
                if (idx <= 0) Cfg.MicDevice = -1;
                else if (idx - 1 < _devices.Count) Cfg.MicDevice = _devices[idx - 1].Index;
                Cfg.Save();
                Log.Info("麦克风设备已切换：" + _micCombo.Text);
            };
            sp.Children.Add(W.Inline(Label("麦克风设备"), _micCombo, 96));

            List<object> rates = new List<object>();
            rates.Add("48000 Hz（推荐）");
            rates.Add("44100 Hz");
            _rateCombo = W.Combo(rates, Cfg.SampleRate == 44100 ? 1 : 0);
            _rateCombo.Width = 150;
            _rateCombo.SelectionChanged += delegate
            {
                Cfg.SampleRate = _rateCombo.SelectedIndex == 1 ? 44100 : 48000;
                Cfg.Save();
            };
            sp.Children.Add(W.Inline(Label("采样率"), _rateCombo, 96));

            StackPanel latRow = new StackPanel();
            latRow.Orientation = Orientation.Horizontal;
            _latencyBox = W.Text(Cfg.LatencyMs.ToString(), 74);
            _latencyBox.TextChanged += delegate
            {
                int v;
                if (int.TryParse(_latencyBox.Text, out v) && v >= 0 && v <= 1000)
                {
                    Cfg.LatencyMs = v;
                    Cfg.Save();
                }
            };
            latRow.Children.Add(_latencyBox);
            TextBlock msUnit = UI.T("毫秒", 12, Theme.Sub, Theme.Normal);
            msUnit.Margin = new Thickness(8, 0, 16, 0);
            msUnit.VerticalAlignment = VerticalAlignment.Center;
            latRow.Children.Add(msUnit);

            _autoCal = W.Check("录制时自动估算延迟", Cfg.AutoCalibrate);
            _autoCal.VerticalAlignment = VerticalAlignment.Center;
            _autoCal.Checked += delegate { Cfg.AutoCalibrate = true; Cfg.Save(); };
            _autoCal.Unchecked += delegate { Cfg.AutoCalibrate = false; Cfg.Save(); };
            latRow.Children.Add(_autoCal);
            sp.Children.Add(W.Inline(Label("延迟补偿"), latRow, 96));

            _sysAudio = W.Check("采集系统声音（视频原声）", Cfg.CaptureSystemAudio);
            _sysAudio.Checked += delegate { Cfg.CaptureSystemAudio = true; Cfg.Save(); };
            _sysAudio.Unchecked += delegate { Cfg.CaptureSystemAudio = false; Cfg.Save(); };
            sp.Children.Add(W.Inline(Label("声音来源"), _sysAudio, 96));

            StackPanel fpsRow = new StackPanel();
            fpsRow.Orientation = Orientation.Horizontal;
            _fpsSlider = W.Slide(4, AppInfo.MaxFpsLimit, Cfg.Fps, 190);
            _fpsLabel = UI.T(Cfg.Fps + " fps", 12, Theme.Text, Theme.Bold);
            _fpsLabel.Width = 64;
            _fpsLabel.Margin = new Thickness(10, 0, 0, 0);
            _fpsLabel.VerticalAlignment = VerticalAlignment.Center;
            _fpsSlider.ValueChanged += delegate
            {
                Cfg.Fps = (int)_fpsSlider.Value;
                _fpsLabel.Text = Cfg.Fps + " fps";
                Cfg.Save();
            };
            fpsRow.Children.Add(_fpsSlider);
            fpsRow.Children.Add(_fpsLabel);
            sp.Children.Add(W.Inline(Label("采集帧率"), fpsRow, 96));

            List<object> widths = new List<object>();
            widths.Add("960（省资源）");
            widths.Add("1280（推荐）");
            widths.Add("1600");
            widths.Add("1920（原画）");
            int wi = 1;
            if (Cfg.MaxWidth == 960) wi = 0;
            else if (Cfg.MaxWidth == 1600) wi = 2;
            else if (Cfg.MaxWidth == 1920) wi = 3;
            _widthCombo = W.Combo(widths, wi);
            _widthCombo.Width = 170;
            _widthCombo.SelectionChanged += delegate
            {
                int[] vals = new int[] { 960, 1280, 1600, 1920 };
                int idx = _widthCombo.SelectedIndex;
                Cfg.MaxWidth = vals[Math.Max(0, Math.Min(3, idx))];
                Cfg.Save();
            };
            sp.Children.Add(W.Inline(Label("画面宽度"), _widthCombo, 96));

            StackPanel qRow = new StackPanel();
            qRow.Orientation = Orientation.Horizontal;
            _qualitySlider = W.Slide(40, 95, Cfg.JpegQuality, 190);
            _qualityLabel = UI.T(Cfg.JpegQuality + " %", 12, Theme.Text, Theme.Bold);
            _qualityLabel.Width = 64;
            _qualityLabel.Margin = new Thickness(10, 0, 0, 0);
            _qualityLabel.VerticalAlignment = VerticalAlignment.Center;
            _qualitySlider.ValueChanged += delegate
            {
                Cfg.JpegQuality = (int)_qualitySlider.Value;
                _qualityLabel.Text = Cfg.JpegQuality + " %";
                Cfg.Save();
            };
            qRow.Children.Add(_qualitySlider);
            qRow.Children.Add(_qualityLabel);
            sp.Children.Add(W.Inline(Label("画面质量"), qRow, 96));

            TextBlock tip = UI.T("提示：录制时先开「开始检测」，软件会用预录缓冲把视频开始前的 0.8 秒一并写入，做到录制起点与播放起点对齐；延迟补偿用于抵消麦克风的采集延迟。",
                11.5, Theme.Muted(), Theme.Normal);
            tip.Margin = new Thickness(0, 10, 0, 0);
            tip.LineHeight = 18;
            sp.Children.Add(tip);

            card.Child = sp;
            root.Children.Add(card);

            // ---------- 保存位置 ----------
            Border outCard = UI.Card(18);
            StackPanel osp = new StackPanel();
            osp.Children.Add(H2("保存位置"));

            StackPanel pathRow = new StackPanel();
            pathRow.Orientation = Orientation.Horizontal;
            _outBox = W.Text(Cfg.OutDir, 420);
            _outBox.TextChanged += delegate
            {
                if (_outBox.Text.Trim().Length > 0) { Cfg.OutDir = _outBox.Text.Trim(); Cfg.Save(); }
            };
            pathRow.Children.Add(_outBox);

            FlatButton browse = new FlatButton("浏览…", BtnKind.Secondary);
            browse.Height = 30;
            browse.MinHeight = 30;
            browse.Margin = new Thickness(10, 0, 0, 0);
            browse.Click += delegate
            {
                try
                {
                    System.Windows.Forms.FolderBrowserDialog d = new System.Windows.Forms.FolderBrowserDialog();
                    d.Description = "选择录制文件的保存目录";
                    d.SelectedPath = Cfg.OutDir;
                    if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _outBox.Text = d.SelectedPath;
                    }
                }
                catch (Exception ex) { Log.Warn("选择目录失败：" + ex.Message); }
            };
            pathRow.Children.Add(browse);

            FlatButton openDir = new FlatButton("打开", BtnKind.Ghost);
            openDir.Height = 30;
            openDir.MinHeight = 30;
            openDir.Margin = new Thickness(8, 0, 0, 0);
            openDir.Click += delegate
            {
                try { Directory.CreateDirectory(Cfg.OutDir); Process.Start("explorer.exe", "\"" + Cfg.OutDir + "\""); }
                catch { }
            };
            pathRow.Children.Add(openDir);

            osp.Children.Add(pathRow);
            outCard.Child = osp;
            root.Children.Add(outCard);

            // ---------- 录制历史 ----------
            Border hisCard = UI.Card(18);
            Grid hg = new Grid();
            RowDefinition hr0 = new RowDefinition();
            hr0.Height = GridLength.Auto;
            RowDefinition hr1 = new RowDefinition();
            hr1.Height = new GridLength(1, GridUnitType.Star);
            hg.RowDefinitions.Add(hr0);
            hg.RowDefinitions.Add(hr1);

            Grid head = new Grid();
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = GridLength.Auto;
            head.ColumnDefinitions.Add(c0);
            head.ColumnDefinitions.Add(c1);
            head.Children.Add(H2("录制历史"));

            FlatButton refresh = new FlatButton("刷新", BtnKind.Ghost);
            refresh.Height = 26;
            refresh.MinHeight = 26;
            refresh.Margin = new Thickness(0, 0, 0, 6);
            refresh.Click += delegate { LoadHistory(); };
            Grid.SetColumn(refresh, 1);
            head.Children.Add(refresh);
            Grid.SetRow(head, 0);
            hg.Children.Add(head);

            ScrollViewer hsv = new ScrollViewer();
            hsv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            hsv.MaxHeight = 240;
            _historyList = new StackPanel();
            hsv.Content = _historyList;
            Grid.SetRow(hsv, 1);
            hg.Children.Add(hsv);

            hisCard.Child = hg;
            root.Children.Add(hisCard);

            Children.Add(sv);
            LoadHistory();
        }

        private TextBlock Label(string text)
        {
            TextBlock t = UI.T(text, 12.5, Theme.Sub, Theme.Normal);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Margin = new Thickness(0, 0, 10, 0);
            return t;
        }

        public override void OnShown()
        {
            LoadHistory();
        }

        private void LoadHistory()
        {
            if (_historyList == null) return;
            _historyList.Children.Clear();
            List<FileInfo> files = new List<FileInfo>();
            try
            {
                if (Directory.Exists(Cfg.OutDir))
                {
                    foreach (string f in Directory.GetFiles(Cfg.OutDir, "*.avi")) files.Add(new FileInfo(f));
                }
            }
            catch { }
            files.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });

            if (files.Count == 0)
            {
                TextBlock empty = UI.T("还没有录制文件，开始检测后会自动生成。", 12, Theme.Muted(), Theme.Normal);
                empty.Margin = new Thickness(0, 8, 0, 8);
                _historyList.Children.Add(empty);
                return;
            }

            int shown = 0;
            foreach (FileInfo f in files)
            {
                if (shown >= 30) break;
                shown++;
                _historyList.Children.Add(HistoryRow(f));
            }
        }

        private UIElement HistoryRow(FileInfo f)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 5, 0, 5);
            g.Background = new SolidColorBrush(Colors.Transparent);
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = GridLength.Auto;
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            StackPanel left = new StackPanel();
            TextBlock name = UI.T(f.Name, 12.5, Theme.Text, Theme.Bold);
            left.Children.Add(name);
            TextBlock meta = UI.T(f.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + "　" + (f.Length / 1024 / 1024) + " MB",
                11, Theme.Muted(), Theme.Normal);
            meta.Margin = new Thickness(0, 2, 0, 0);
            left.Children.Add(meta);
            g.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.VerticalAlignment = VerticalAlignment.Center;

            FlatButton play = new FlatButton("播放", BtnKind.Secondary);
            play.Height = 26;
            play.MinHeight = 26;
            play.Width = 62;
            play.Click += delegate
            {
                try { Process.Start("\"" + f.FullName + "\""); }
                catch (Exception ex) { Log.Warn("无法播放：" + ex.Message); }
            };
            right.Children.Add(play);

            FlatButton del = new FlatButton("删除", BtnKind.Danger);
            del.Height = 26;
            del.MinHeight = 26;
            del.Width = 62;
            del.Margin = new Thickness(8, 0, 0, 0);
            del.Click += delegate
            {
                try
                {
                    File.Delete(f.FullName);
                    Log.Info("已删除录制文件：" + f.Name);
                    LoadHistory();
                }
                catch (Exception ex) { Log.Warn("删除失败：" + ex.Message); }
            };
            right.Children.Add(del);

            Grid.SetColumn(right, 1);
            g.Children.Add(right);

            Border wrap = new Border();
            wrap.Child = g;
            wrap.BorderBrush = Theme.Border;
            wrap.BorderThickness = new Thickness(0, 0, 0, 1);
            wrap.Padding = new Thickness(0, 2, 0, 6);
            return wrap;
        }
    }
}
