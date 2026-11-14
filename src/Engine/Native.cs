using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Rap.Studio.Engine
{
    /// <summary>Win32 互操作声明。</summary>
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        public const int PW_CLIENTONLY = 1;
        public const int PW_RENDERFULLCONTENT = 2;

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int count);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        /// <summary>Win11 圆角窗口：DWMWA_WINDOW_CORNER_PREFERENCE = 33。</summary>
        public static void TryRoundCorners(IntPtr hwnd)
        {
            try
            {
                int pref = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(hwnd, 33, ref pref, sizeof(int));
            }
            catch { }
        }

        public static string WindowTitle(IntPtr hwnd)
        {
            try
            {
                int len = GetWindowTextLength(hwnd);
                if (len <= 0) return string.Empty;
                StringBuilder sb = new StringBuilder(len + 2);
                GetWindowText(hwnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch { return string.Empty; }
        }

        public static string WindowClass(IntPtr hwnd)
        {
            try
            {
                StringBuilder sb = new StringBuilder(256);
                GetClassName(hwnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch { return string.Empty; }
        }
    }
}
