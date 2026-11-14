using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Threading;

namespace Rap.Studio
{
    public enum LogKind { Info, Good, Warn, Error }

    public class LogEntry
    {
        public DateTime Time;
        public string Text;
        public LogKind Kind;
    }

    /// <summary>应用内日志总线：所有模块往这里写，界面订阅显示。</summary>
    public class LogBus
    {
        private readonly object _lock = new object();
        private readonly List<LogEntry> _items = new List<LogEntry>();

        public event Action<LogEntry> OnLine;

        public IList<LogEntry> Items
        {
            get
            {
                lock (_lock) return new List<LogEntry>(_items);
            }
        }

        public void Write(string text, LogKind kind)
        {
            LogEntry e = new LogEntry();
            e.Time = DateTime.Now;
            e.Text = text;
            e.Kind = kind;
            lock (_lock)
            {
                _items.Add(e);
                if (_items.Count > 500) _items.RemoveAt(0);
            }
            Action<LogEntry> h = OnLine;
            if (h != null) h(e);
        }

        public void Info(string t) { Write(t, LogKind.Info); }
        public void Good(string t) { Write(t, LogKind.Good); }
        public void Warn(string t) { Write(t, LogKind.Warn); }
        public void Error(string t) { Write(t, LogKind.Error); }
    }

    /// <summary>把后台线程的回调安全地切回 UI 线程。</summary>
    public static class Ui
    {
        public static Dispatcher D;

        public static void Post(Action a)
        {
            if (a == null) return;
            if (D == null) { a(); return; }
            if (D.CheckAccess()) a();
            else D.BeginInvoke(a, DispatcherPriority.Background);
        }
    }
}
