using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rap.Studio.Engine;

namespace Rap.Studio
{
    public class PageSettings : PageBase
    {
        private Slider _thrSlider;
        private TextBlock _thrLabel;
        private Slider _silenceSlider;
        private TextBlock _silenceLabel;
        private Slider _startSlider;
        private TextBlock _startLabel;
        private CheckBox _autoRecord;
        private CheckBox _keepLog;

        public PageSettings(AppHost host) : base(host)
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

            Border card = UI.Card(18);
            StackPanel sp = new StackPanel();
            sp.Children.Add(H2("检测判定"));

            StackPanel thrRow = new StackPanel();
            thrRow.Orientation = Orientation.Horizontal;
            _thrSlider = W.Slide(0.004, 0.16, Cfg.SilenceThreshold, 210);
            _thrSlider.IsSnapToTickEnabled = false;
            _thrLabel = UI.T(FmtThr(Cfg.SilenceThreshold), 12, Theme.Text, Theme.Bold);
            _thrLabel.Width = 80;
            _thrLabel.VerticalAlignment = VerticalAlignment.Center;
            _thrLabel.Margin = new Thickness(10, 0, 0, 0);
            _thrSlider.ValueChanged += delegate
            {
                Cfg.SilenceThreshold = _thrSlider.Value;
                _thrLabel.Text = FmtThr(Cfg.SilenceThreshold);
                Cfg.Save();
            };
            thrRow.Children.Add(_thrSlider);
            thrRow.Children.Add(_thrLabel);
            sp.Children.Add(W.Inline(Label("声音阈值"), thrRow, 96));

            StackPanel silRow = new StackPanel();
            silRow.Orientation = Orientation.Horizontal;
            _silenceSlider = W.Slide(800, 6000, Cfg.SilenceMs, 210);
            _silenceSlider.TickFrequency = 200;
            _silenceLabel = UI.T(Cfg.SilenceMs + " ms", 12, Theme.Text, Theme.Bold);
            _silenceLabel.Width = 80;
            _silenceLabel.VerticalAlignment = VerticalAlignment.Center;
            _silenceLabel.Margin = new Thickness(10, 0, 0, 0);
            _silenceSlider.ValueChanged += delegate
            {
                Cfg.SilenceMs = (int)(_silenceSlider.Value / 200) * 200;
                _silenceLabel.Text = Cfg.SilenceMs + " ms";
                Cfg.Save();
            };
            silRow.Children.Add(_silenceSlider);
            silRow.Children.Add(_silenceLabel);
            sp.Children.Add(W.Inline(Label("静音判定"), silRow, 96));

            StackPanel stRow = new StackPanel();
            stRow.Orientation = Orientation.Horizontal;
            _startSlider = W.Slide(200, 1500, Cfg.StartHoldMs, 210);
            _startSlider.TickFrequency = 100;
            _startLabel = UI.T(Cfg.StartHoldMs + " ms", 12, Theme.Text, Theme.Bold);
            _startLabel.Width = 80;
            _startLabel.VerticalAlignment = VerticalAlignment.Center;
            _startLabel.Margin = new Thickness(10, 0, 0, 0);
            _startSlider.ValueChanged += delegate
            {
                Cfg.StartHoldMs = (int)(_startSlider.Value / 100) * 100;
                _startLabel.Text = Cfg.StartHoldMs + " ms";
                Cfg.Save();
            };
            stRow.Children.Add(_startSlider);
            stRow.Children.Add(_startLabel);
            sp.Children.Add(W.Inline(Label("起播判定"), stRow, 96));

            TextBlock tip = UI.T("声音阈值越小越灵敏；静音判定时长决定「安静多久算视频结束」；起播判定时长决定「声音持续多久算视频开始」。",
                11.5, Theme.Muted(), Theme.Normal);
            tip.Margin = new Thickness(0, 8, 0, 0);
            tip.LineHeight = 18;
            sp.Children.Add(tip);
            card.Child = sp;
            root.Children.Add(card);

            Border card2 = UI.Card(18);
            StackPanel sp2 = new StackPanel();
            sp2.Children.Add(H2("行为"));
            _autoRecord = W.Check("检测到视频开始时自动录制", Cfg.AutoRecord);
            _autoRecord.Checked += delegate { Cfg.AutoRecord = true; Cfg.Save(); };
            _autoRecord.Unchecked += delegate { Cfg.AutoRecord = false; Cfg.Save(); };
            sp2.Children.Add(_autoRecord);

            _keepLog = W.Check("保留运行日志（写入配置目录）", Cfg.KeepLog);
            _keepLog.Checked += delegate { Cfg.KeepLog = true; Cfg.Save(); };
            _keepLog.Unchecked += delegate { Cfg.KeepLog = false; Cfg.Save(); };
            sp2.Children.Add(_keepLog);

            sp2.Children.Add(W.Inline(Label("界面语言"), UI.T("简体中文（仅支持）", 12.5, Theme.Text, Theme.Normal), 96));
            card2.Child = sp2;
            root.Children.Add(card2);

            Border card3 = UI.Card(18);
            StackPanel sp3 = new StackPanel();
            sp3.Children.Add(H2("目录与维护"));
            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;

            FlatButton cfgDir = new FlatButton("配置目录", BtnKind.Secondary);
            cfgDir.Height = 30;
            cfgDir.MinHeight = 30;
            cfgDir.Click += delegate
            {
                try { Process.Start("explorer.exe", "\"" + AppInfo.DataDir + "\""); } catch { }
            };
            row.Children.Add(cfgDir);

            FlatButton outDir = new FlatButton("录制目录", BtnKind.Secondary);
            outDir.Height = 30;
            outDir.MinHeight = 30;
            outDir.Margin = new Thickness(8, 0, 0, 0);
            outDir.Click += delegate
            {
                try { Directory.CreateDirectory(Cfg.OutDir); Process.Start("explorer.exe", "\"" + Cfg.OutDir + "\""); } catch { }
            };
            row.Children.Add(outDir);

            FlatButton reset = new FlatButton("恢复默认设置", BtnKind.Danger);
            reset.Height = 30;
            reset.MinHeight = 30;
            reset.Margin = new Thickness(8, 0, 0, 0);
            reset.Click += delegate { ResetDefaults(); };
            row.Children.Add(reset);

            sp3.Children.Add(row);
            card3.Child = sp3;
            root.Children.Add(card3);

            Children.Add(sv);
        }

        private void ResetDefaults()
        {
            Config d = new Config();
            Cfg.SilenceThreshold = d.SilenceThreshold;
            Cfg.SilenceMs = d.SilenceMs;
            Cfg.StartHoldMs = d.StartHoldMs;
            Cfg.Fps = d.Fps;
            Cfg.JpegQuality = d.JpegQuality;
            Cfg.MaxWidth = d.MaxWidth;
            Cfg.LatencyMs = d.LatencyMs;
            Cfg.AutoCalibrate = d.AutoCalibrate;
            Cfg.CaptureSystemAudio = d.CaptureSystemAudio;
            Cfg.OcrEnabled = d.OcrEnabled;
            Cfg.OcrIntervalMs = d.OcrIntervalMs;
            Cfg.OcrRegion = d.OcrRegion;
            Cfg.AutoRecord = d.AutoRecord;
            Cfg.ThemeFollow = d.ThemeFollow;
            Cfg.Save();

            _thrSlider.Value = Cfg.SilenceThreshold;
            _silenceSlider.Value = Cfg.SilenceMs;
            _startSlider.Value = Cfg.StartHoldMs;
            _autoRecord.IsChecked = Cfg.AutoRecord;

            Log.Info("已恢复默认设置");
            if (Host.Window != null) Toast.Show(Host.Window, "已恢复默认设置", true);
        }

        private static string FmtThr(double v)
        {
            return (v * 100).ToString("0.0") + " %";
        }

        private TextBlock Label(string text)
        {
            TextBlock t = UI.T(text, 12.5, Theme.Sub, Theme.Normal);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Margin = new Thickness(0, 0, 10, 0);
            return t;
        }
    }
}
