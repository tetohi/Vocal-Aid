using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rap.Studio
{
    /// <summary>自用版专属：抓取 B 站视频资料 / 网易云歌词，结果可保存到本地。</summary>
    public class PageCrawler : PageBase
    {
        private TextBox _input;
        private TextBox _out;
        private TextBlock _status;
        private ComboBox _mode;

        public PageCrawler(AppHost host) : base(host)
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

            StackPanel sp = new StackPanel();
            sp.Children.Add(H2("爬虫 · 抓取资料"));
            sp.Children.Add(Body("粘贴 B 站链接 / BV 号抓视频资料与热门评论；输入歌名抓网易云歌词。"));

            StackPanel row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.Margin = new Thickness(0, 12, 0, 0);

            _mode = W.Combo(new string[] { "自动识别", "B 站视频", "网易云歌词" }, 0);
            _mode.Width = 132;
            row.Children.Add(_mode);

            _input = W.Text("", 380);
            _input.Height = 32;
            _input.Margin = new Thickness(10, 0, 0, 0);
            row.Children.Add(_input);

            FlatButton go = new FlatButton("开始抓取", BtnKind.Primary);
            go.MinWidth = 110;
            go.Margin = new Thickness(10, 2, 0, 0);
            go.Click += delegate { Fetch(); };
            row.Children.Add(go);

            FlatButton save = new FlatButton("保存到本地", BtnKind.Secondary);
            save.MinWidth = 110;
            save.Margin = new Thickness(10, 2, 0, 0);
            save.Click += delegate { Save(); };
            row.Children.Add(save);
            sp.Children.Add(row);

            _status = UI.T("等待输入", 11.5, Theme.Muted(), Theme.Normal);
            _status.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(_status);
            root.Children.Add(Card(sp, 16));

            StackPanel rp = new StackPanel();
            rp.Children.Add(H2("抓取结果"));
            _out = new TextBox();
            _out.IsReadOnly = true;
            _out.AcceptsReturn = true;
            _out.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _out.Height = 380;
            _out.FontFamily = new FontFamily("Microsoft YaHei UI");
            _out.FontSize = 12;
            _out.Background = Brushes.White;
            _out.Foreground = Theme.Text;
            _out.BorderBrush = Theme.Border;
            _out.Padding = new Thickness(10, 8, 10, 8);
            rp.Children.Add(_out);
            root.Children.Add(Card(rp, 16));

            Children.Add(sv);
        }

        private void Fetch()
        {
            string q = _input.Text.Trim();
            if (q.Length == 0) { _status.Text = "请先粘贴链接或输入歌名"; return; }
            _status.Text = "抓取中…";
            int mode = _mode.SelectedIndex;
            Thread t = new Thread(delegate()
            {
                string res;
                try
                {
                    res = Crawler.Go(q, mode);
                }
                catch (Exception ex)
                {
                    res = "抓取失败：" + ex.Message;
                }
                Ui.Post(delegate
                {
                    _out.Text = res;
                    _status.Text = "完成 · " + DateTime.Now.ToString("HH:mm:ss");
                    Log.Info("爬虫抓取：" + q);
                });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void Save()
        {
            if (_out.Text.Length == 0) { _status.Text = "还没有结果可保存"; return; }
            try
            {
                string dir = AppInfo.DefaultOutDir;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string f = Path.Combine(dir, "爬取_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(f, _out.Text, new UTF8Encoding(true));
                _status.Text = "已保存：" + f;
                Log.Info("爬取结果已保存：" + f);
            }
            catch (Exception ex)
            {
                _status.Text = "保存失败：" + ex.Message;
            }
        }
    }

    /// <summary>极简抓取实现：B 站开放接口 + 网易云接口。</summary>
    public static class Crawler
    {
        public static string Go(string input, int mode)
        {
            string bvid = FindBvid(input);
            bool wantBili = mode == 1 || (mode == 0 && bvid.Length > 0);
            if (wantBili)
            {
                if (bvid.Length == 0) throw new Exception("没识别出 BV 号，请粘贴完整视频链接");
                return Bili(bvid);
            }
            return Music(input);
        }

        private static string FindBvid(string s)
        {
            for (int i = 0; i + 12 <= s.Length; i++)
            {
                if (s.Substring(i, 2) == "BV")
                {
                    string tail = s.Substring(i, Math.Min(12, s.Length - i));
                    if (tail.Length == 12) return tail;
                }
            }
            return "";
        }

        private static string Http(string url, string referer)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0 Safari/537.36";
            req.Timeout = 12000;
            req.ReadWriteTimeout = 15000;
            if (referer != null) req.Referer = referer;
            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        // ---- 极简 JSON 取值（只在对象第一层找键，避开嵌套同名字段）----
        private static string Str(string json, string key)
        {
            string tag = "\"" + key + "\":";
            int depth = 0;
            int i = 0;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '"')
                {
                    if (depth == 1 && i + tag.Length <= json.Length && json.Substring(i, tag.Length) == tag)
                    {
                        return Read(json, i + tag.Length);
                    }
                    int j = i + 1;
                    while (j < json.Length)
                    {
                        if (json[j] == '\\') { j += 2; continue; }
                        if (json[j] == '"') break;
                        j++;
                    }
                    i = j + 1;
                    continue;
                }
                if (c == '{' || c == '[') { depth++; i++; continue; }
                if (c == '}' || c == ']') { depth--; i++; continue; }
                i++;
            }
            return "";
        }

        private static string Read(string json, int i)
        {
            while (i < json.Length && (json[i] == ' ' || json[i] == '\n' || json[i] == '\r')) i++;
            if (i >= json.Length) return "";
            if (json[i] == '"')
            {
                StringBuilder sb = new StringBuilder();
                i++;
                while (i < json.Length)
                {
                    char c = json[i];
                    if (c == '\\')
                    {
                        if (i + 1 < json.Length)
                        {
                            char n = json[i + 1];
                            if (n == 'n') sb.Append('\n');
                            else if (n == 't') sb.Append('\t');
                            else if (n == 'r') { }
                            else if (n == 'u')
                            {
                                if (i + 5 < json.Length)
                                {
                                    try { sb.Append((char)Convert.ToInt32(json.Substring(i + 2, 4), 16)); } catch { }
                                    i += 4;
                                }
                            }
                            else sb.Append(n);
                        }
                        i += 2;
                        continue;
                    }
                    if (c == '"') break;
                    sb.Append(c);
                    i++;
                }
                return sb.ToString();
            }
            StringBuilder num = new StringBuilder();
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-' || json[i] == '.')) { num.Append(json[i]); i++; }
            return num.ToString();
        }

        private static string Obj(string json, string key)
        {
            string tag = "\"" + key + "\":{";
            int i = json.IndexOf(tag);
            if (i < 0) return "";
            int depth = 0;
            int start = i + tag.Length - 1;
            for (int j = start; j < json.Length; j++)
            {
                if (json[j] == '{') depth++;
                else if (json[j] == '}')
                {
                    depth--;
                    if (depth == 0) return json.Substring(start, j - start + 1);
                }
            }
            return "";
        }

        private static string FirstObj(string arr)
        {
            int i = arr.IndexOf('{');
            if (i < 0) return "";
            int depth = 0;
            for (int j = i; j < arr.Length; j++)
            {
                if (arr[j] == '{') depth++;
                else if (arr[j] == '}')
                {
                    depth--;
                    if (depth == 0) return arr.Substring(i, j - i + 1);
                }
            }
            return "";
        }

        private static string Arr(string json, string key)
        {
            string tag = "\"" + key + "\":[";
            int i = json.IndexOf(tag);
            if (i < 0) return "";
            int depth = 0;
            int start = i + tag.Length - 1;
            for (int j = start; j < json.Length; j++)
            {
                if (json[j] == '[') depth++;
                else if (json[j] == ']')
                {
                    depth--;
                    if (depth == 0) return json.Substring(start, j - start + 1);
                }
            }
            return "";
        }

        private static string Clock(int sec)
        {
            if (sec <= 0) return "-";
            return (sec / 60) + " 分 " + (sec % 60) + " 秒";
        }

        private static string Bili(string bvid)
        {
            string url = "https://api.bilibili.com/x/web-interface/view?bvid=" + bvid;
            string j = Http(url, "https://www.bilibili.com");
            if (Str(j, "code") != "0") throw new Exception("接口返回错误：" + Str(j, "message"));
            string data = Obj(j, "data");
            string owner = Obj(data, "owner");
            string stat = Obj(data, "stat");
            int dur = int.Parse(string.IsNullOrEmpty(Str(data, "duration")) ? "0" : Str(data, "duration"));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("【B 站视频】");
            sb.AppendLine("标题：" + Str(data, "title"));
            sb.AppendLine("BV 号：" + Str(data, "bvid"));
            sb.AppendLine("UP 主：" + Str(owner, "name"));
            sb.AppendLine("时长：" + Clock(dur));
            sb.AppendLine("分区：" + Str(data, "tname"));
            sb.AppendLine("播放：" + Str(stat, "view") + "   弹幕：" + Str(stat, "danmaku") + "   点赞：" + Str(stat, "like") + "   收藏：" + Str(stat, "favorite"));
            sb.AppendLine();
            sb.AppendLine("简介：");
            sb.AppendLine(Str(data, "desc"));

            try
            {
                string aid = Str(data, "aid");
                string rj = Http("https://api.bilibili.com/x/v2/reply?type=1&oid=" + aid + "&sort=2&ps=20&pn=1", "https://www.bilibili.com");
                string replies = Arr(rj, "replies");
                if (replies.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("热门评论：");
                    int idx = 0;
                    int pos = 0;
                    while (idx < 20)
                    {
                        int p = replies.IndexOf("\"message\":", pos);
                        if (p < 0) break;
                        string msg = Str(replies.Substring(p), "message");
                        msg = msg.Replace("\r", " ").Replace("\n", " ").Trim();
                        if (msg.Length > 0)
                        {
                            idx++;
                            sb.AppendLine("  " + idx + ". " + msg);
                        }
                        pos = p + 10;
                    }
                }
            }
            catch { sb.AppendLine(); sb.AppendLine("（评论未取到，接口需要登录态）"); }
            return sb.ToString();
        }

        private static string Music(string keyword)
        {
            string q = Uri.EscapeDataString(keyword);
            string j = Http("https://music.163.com/api/search/get/web?s=" + q + "&type=1&limit=5", "https://music.163.com");
            string songs = Arr(Obj(j, "result"), "songs");
            if (songs.Length == 0) throw new Exception("没搜到这首歌");
            string first = FirstObj(songs);
            string id = Str(first, "id");
            string name = Str(first, "name");
            string artists = Arr(first, "artists");
            string ar = FirstObj(artists);
            string singer = Str(ar, "name");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("【网易云歌词】");
            sb.AppendLine("歌曲：" + name + "    歌手：" + singer + "    歌曲 ID：" + id);
            sb.AppendLine();
            try
            {
                string lj = Http("https://music.163.com/api/song/lyric?id=" + id + "&lv=-1&kv=-1&tv=-1", "https://music.163.com");
                string lrc = Str(Obj(lj, "lrc"), "lyric");
                if (lrc.Length == 0) lrc = Str(lj, "lyric");
                sb.AppendLine(Plain(lrc));
            }
            catch (Exception ex) { sb.AppendLine("歌词获取失败：" + ex.Message); }
            return sb.ToString();
        }

        private static string Plain(string lrc)
        {
            if (lrc.Length == 0) return "（无歌词）";
            StringBuilder sb = new StringBuilder();
            string[] lines = lrc.Replace("\r", "").Split('\n');
            foreach (string line in lines)
            {
                string t = line;
                while (t.StartsWith("[") && t.IndexOf(']') > 0) t = t.Substring(t.IndexOf(']') + 1);
                t = t.Trim();
                if (t.Length > 0) sb.AppendLine(t);
            }
            return sb.ToString();
        }
    }
}
