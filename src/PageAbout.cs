using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rap.Studio.Engine;

namespace Rap.Studio
{
    public class PageAbout : PageBase
    {
        public PageAbout(AppHost host) : base(host)
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

            // ---------- 头部 ----------
            Border head = UI.Card(22);
            Grid hg = new Grid();
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = GridLength.Auto;
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c2 = new ColumnDefinition();
            c2.Width = GridLength.Auto;
            hg.ColumnDefinitions.Add(c0);
            hg.ColumnDefinitions.Add(c1);
            hg.ColumnDefinitions.Add(c2);

            Grid logoBox = new Grid();
            logoBox.Width = 64;
            logoBox.Height = 64;
            logoBox.VerticalAlignment = VerticalAlignment.Center;
            logoBox.Children.Add(IconDraw.Logo(64));
            hg.Children.Add(logoBox);

            StackPanel info = new StackPanel();
            info.Margin = new Thickness(18, 0, 0, 0);
            info.VerticalAlignment = VerticalAlignment.Center;

            StackPanel nameRow = new StackPanel();
            nameRow.Orientation = Orientation.Horizontal;
            TextBlock name = UI.T(AppInfo.DisplayName, 20, Theme.Text, Theme.Heavy);
            nameRow.Children.Add(name);
            Border verChip = UI.Pill("v" + AppInfo.Version, Theme.AccentDeep, Theme.AccentSoft);
            verChip.Margin = new Thickness(10, 3, 0, 0);
            verChip.VerticalAlignment = VerticalAlignment.Center;
            nameRow.Children.Add(verChip);

            Border edChip = UI.Pill(AppInfo.Edition, Theme.Sub, new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7)));
            edChip.Margin = new Thickness(6, 3, 0, 0);
            edChip.VerticalAlignment = VerticalAlignment.Center;
            nameRow.Children.Add(edChip);
            info.Children.Add(nameRow);

            TextBlock sub = UI.T("Rap Streamer Assistant　·　说唱主播的浏览器视频检测 · 音画同步录制 · 实时台词工具",
                12, Theme.Sub, Theme.Normal);
            sub.Margin = new Thickness(0, 8, 0, 0);
            info.Children.Add(sub);

            TextBlock stamp = UI.T(AppInfo.BuildStamp + "　·　运行时 .NET Framework 4.8　·　语言 简体中文",
                11, Theme.Muted(), Theme.Normal);
            stamp.Margin = new Thickness(0, 5, 0, 0);
            info.Children.Add(stamp);

            Grid.SetColumn(info, 1);
            hg.Children.Add(info);

            StackPanel ops = new StackPanel();
            ops.VerticalAlignment = VerticalAlignment.Center;
            FlatButton openApp = new FlatButton("程序目录", BtnKind.Secondary);
            openApp.Height = 30;
            openApp.MinHeight = 30;
            openApp.Width = 96;
            openApp.Click += delegate
            {
                try { Process.Start("explorer.exe", "\"" + AppDomain.CurrentDomain.BaseDirectory + "\""); } catch { }
            };
            ops.Children.Add(openApp);

            FlatButton openCfg = new FlatButton("配置目录", BtnKind.Ghost);
            openCfg.Height = 30;
            openCfg.MinHeight = 30;
            openCfg.Width = 96;
            openCfg.Margin = new Thickness(0, 6, 0, 0);
            openCfg.Click += delegate
            {
                try { Process.Start("explorer.exe", "\"" + AppInfo.DataDir + "\""); } catch { }
            };
            ops.Children.Add(openCfg);
            Grid.SetColumn(ops, 2);
            hg.Children.Add(ops);

            head.Child = hg;
            root.Children.Add(head);

            // ---------- 功能 ----------
            Border feat = UI.Card(18);
            StackPanel fs = new StackPanel();
            fs.Children.Add(H2("主要功能"));
            fs.Children.Add(FeatureRow("1", "浏览器状态检测", "自动识别 Chrome / Edge / Firefox 等浏览器窗口，判断当前是否在播放 B 站、抖音、快手等平台的视频。"));
            fs.Children.Add(FeatureRow("2", "视频开始 / 结束自动判定", "通过系统音频电平与播放状态联动判断，视频开始即开录、结束即停录，无需手动点击。"));
            fs.Children.Add(FeatureRow("3", "音画同步录制", "同时采集浏览器画面、系统原声与麦克风，内置 0.8 秒预录缓冲消除开录延迟，并按延迟补偿对齐音画。"));
            fs.Children.Add(FeatureRow("4", "实时台词", "调用 Windows 本地 OCR 识别画面字幕，在软件内实时输出，背景颜色取自当前视频画面主色。"));
            feat.Child = fs;
            root.Children.Add(feat);

            // ---------- 版本 ----------
            Border ed = UI.Card(18);
            StackPanel es = new StackPanel();
            es.Children.Add(H2("版本说明"));
            if (AppInfo.IsPro)
            {
                es.Children.Add(Bullet("自用版：完整的私有构建，帧率上限 60fps，支持台词纠错词典与更高画面宽度。"));
                es.Children.Add(Bullet("标准版：同一套源码的公开构建，帧率上限 15fps，不含私有增强项。"));
            }
            else
            {
                es.Children.Add(Bullet("标准版：基于同一套源码的公开构建，可用于二次开发与分发。"));
                es.Children.Add(Bullet("自用版：私有构建，解锁 60fps 采集、台词纠错词典等增强功能。"));
            }
            es.Children.Add(Bullet("两版共用同一份检测与录制内核，配置文件互不冲突。"));
            ed.Child = es;
            root.Children.Add(ed);

            // ---------- 使用声明 ----------
            Border note = UI.Card(18);
            StackPanel ns = new StackPanel();
            ns.Children.Add(H2("使用声明"));
            ns.Children.Add(Bullet("本工具仅供个人学习、练唱与作品参考使用，录制内容版权归原作者及原平台所有。"));
            ns.Children.Add(Bullet("请勿将录制内容用于商业用途或公开传播；如需转载、二创发布，请先取得权利人授权。"));
            ns.Children.Add(Bullet("程序仅通过系统屏幕与音频接口采集，不破解、不绕过任何加密或版权保护措施。"));
            note.Child = ns;
            root.Children.Add(note);

            // ---------- 更新日志 ----------
            Border log = UI.Card(18);
            StackPanel ls = new StackPanel();
            ls.Children.Add(H2("更新日志"));
            ls.Children.Add(LogRow("v1.0.0", "首个版本：浏览器检测、音画同步录制、实时台词、主题色背景、首次启动引导。"));
            log.Child = ls;
            root.Children.Add(log);

            Children.Add(sv);
        }

        private UIElement FeatureRow(string num, string title, string desc)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 5, 0, 5);
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(30);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            Border badge = new Border();
            badge.Width = 20;
            badge.Height = 20;
            badge.CornerRadius = new CornerRadius(10);
            badge.Background = Theme.AccentSoft;
            badge.VerticalAlignment = VerticalAlignment.Top;
            TextBlock n = UI.T(num, 11, Theme.AccentDeep, Theme.Bold);
            n.HorizontalAlignment = HorizontalAlignment.Center;
            n.VerticalAlignment = VerticalAlignment.Center;
            badge.Child = n;
            g.Children.Add(badge);

            StackPanel sp = new StackPanel();
            TextBlock t = UI.T(title, 12.5, Theme.Text, Theme.Bold);
            sp.Children.Add(t);
            TextBlock d = UI.T(desc, 11.5, Theme.Sub, Theme.Normal);
            d.LineHeight = 18;
            d.Margin = new Thickness(0, 2, 0, 0);
            sp.Children.Add(d);
            Grid.SetColumn(sp, 1);
            g.Children.Add(sp);

            return g;
        }

        private UIElement Bullet(string text)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 4, 0, 4);
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(16);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            System.Windows.Shapes.Ellipse dot = new System.Windows.Shapes.Ellipse();
            dot.Width = 5;
            dot.Height = 5;
            dot.Fill = Theme.Accent;
            dot.VerticalAlignment = VerticalAlignment.Top;
            dot.Margin = new Thickness(0, 7, 0, 0);
            g.Children.Add(dot);

            TextBlock t = UI.T(text, 12, Theme.Sub, Theme.Normal);
            t.LineHeight = 18;
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            return g;
        }

        private UIElement LogRow(string ver, string text)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 3, 0, 3);
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(64);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            TextBlock v = UI.T(ver, 12, Theme.AccentDeep, Theme.Bold);
            g.Children.Add(v);
            TextBlock t = UI.T(text, 12, Theme.Sub, Theme.Normal);
            t.LineHeight = 18;
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            return g;
        }
    }
}
