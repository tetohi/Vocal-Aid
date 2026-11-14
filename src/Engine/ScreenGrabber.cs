using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Rap.Studio.Engine
{
    /// <summary>抓取浏览器窗口画面（PrintWindow 优先，失败则回退为屏幕区域复制）。</summary>
    public class WindowCapturer
    {
        private Bitmap _raw;
        private Graphics _g;
        private Bitmap _scaled;
        private int _w, _h;
        private static byte[] _probe = new byte[0];

        public static Bitmap Capture(IntPtr hwnd, int maxWidth)
        {
            try
            {
                Native.RECT wr, cr;
                if (!Native.GetWindowRect(hwnd, out wr)) return null;
                if (!Native.GetClientRect(hwnd, out cr)) return null;
                int ww = wr.Right - wr.Left, wh = wr.Bottom - wr.Top;
                int cw = cr.Right - cr.Left, ch = cr.Bottom - cr.Top;
                if (ww <= 0 || wh <= 0 || cw <= 0 || ch <= 0) return null;
                if (Native.IsIconic(hwnd)) return null;

                Bitmap full = new Bitmap(ww, wh, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(full))
                {
                    IntPtr hdc = g.GetHdc();
                    bool ok = false;
                    try { ok = Native.PrintWindow(hwnd, hdc, (uint)Native.PW_RENDERFULLCONTENT); }
                    catch { ok = false; }
                    g.ReleaseHdc(hdc);
                    if (!ok || MostlyBlank(full)) { full.Dispose(); return ScreenCopy(hwnd, cr, maxWidth); }
                }

                Native.POINT pt = new Native.POINT();
                pt.X = 0; pt.Y = 0;
                Native.ClientToScreen(hwnd, ref pt);
                int ox = pt.X - wr.Left;
                int oy = pt.Y - wr.Top;
                Rectangle src = new Rectangle(ox, oy, cw, ch);
                src.Intersect(new Rectangle(0, 0, ww, wh));
                if (src.Width <= 4 || src.Height <= 4) { full.Dispose(); return ScreenCopy(hwnd, cr, maxWidth); }

                Bitmap crop = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(crop))
                {
                    g.DrawImage(full, new Rectangle(0, 0, crop.Width, crop.Height), src, GraphicsUnit.Pixel);
                    g.InterpolationMode = InterpolationMode.Bilinear;
                }
                full.Dispose();
                return Fit(crop, maxWidth);
            }
            catch { return null; }
        }

        private static Bitmap ScreenCopy(IntPtr hwnd, Native.RECT cr, int maxWidth)
        {
            try
            {
                Native.POINT pt = new Native.POINT();
                Native.ClientToScreen(hwnd, ref pt);
                int cw = cr.Right - cr.Left, ch = cr.Bottom - cr.Top;
                if (cw <= 0 || ch <= 0) return null;
                Bitmap bmp = new Bitmap(cw, ch, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(pt.X, pt.Y, 0, 0, new Size(cw, ch), CopyPixelOperation.SourceCopy);
                }
                return Fit(bmp, maxWidth);
            }
            catch { return null; }
        }

        private static Bitmap Fit(Bitmap src, int maxWidth)
        {
            if (maxWidth <= 0 || src.Width <= maxWidth) return src;
            double r = (double)maxWidth / src.Width;
            int w = maxWidth;
            int h = Math.Max(2, (int)Math.Round(src.Height * r));
            Bitmap dst = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, 0, 0, w, h);
            }
            src.Dispose();
            return dst;
        }

        /// <summary>粗略判断画面是否整片空白（PrintWindow 对硬件加速窗口常返回黑图）。</summary>
        private static bool MostlyBlank(Bitmap bmp)
        {
            try
            {
                int step = Math.Max(4, Math.Min(40, bmp.Width / 24));
                int dark = 0, total = 0;
                for (int y = 2; y < bmp.Height - 2; y += step)
                {
                    for (int x = 2; x < bmp.Width - 2; x += step)
                    {
                        Color c = bmp.GetPixel(x, y);
                        total++;
                        if (c.R < 8 && c.G < 8 && c.B < 8) dark++;
                    }
                }
                return total > 20 && dark > total * 0.97;
            }
            catch { return false; }
        }

        public static ImageCodecInfo JpegCodec()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo c in codecs)
            {
                if (c.FormatID == ImageFormat.Jpeg.Guid) return c;
            }
            return null;
        }

        public static byte[] EncodeJpeg(Bitmap bmp, int quality)
        {
            try
            {
                using (EncoderParameters ep = new EncoderParameters(1))
                {
                    ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                    ImageCodecInfo codec = JpegCodec();
                    using (MemoryStream ms = new MemoryStream())
                    {
                        if (codec == null) bmp.Save(ms, ImageFormat.Jpeg);
                        else bmp.Save(ms, codec, ep);
                        return ms.ToArray();
                    }
                }
            }
            catch { return null; }
        }

        /// <summary>计算画面主色（用于台词背景，跟随视频主题色）。</summary>
        public static Color DominantColor(Bitmap bmp)
        {
            try
            {
                int step = Math.Max(2, Math.Min(bmp.Width, bmp.Height) / 48);
                int[] bins = new int[64 * 64];
                int[] rs = new int[64 * 64];
                int[] gs = new int[64 * 64];
                int[] bs = new int[64 * 64];
                for (int y = 0; y < bmp.Height; y += step)
                {
                    for (int x = 0; x < bmp.Width; x += step)
                    {
                        Color c = bmp.GetPixel(x, y);
                        int max = Math.Max(c.R, Math.Max(c.G, c.B));
                        int min = Math.Min(c.R, Math.Min(c.G, c.B));
                        int sat = max - min;
                        if (sat < 26) continue;      // 跳过灰阶
                        if (max < 26 || min > 235) continue;
                        int key = ((c.R >> 2) << 12) | ((c.G >> 2) << 6) | (c.B >> 2);
                        bins[key]++;
                        rs[key] += c.R; gs[key] += c.G; bs[key] += c.B;
                    }
                }
                int best = -1, bestCount = 0;
                for (int i = 0; i < bins.Length; i++)
                {
                    if (bins[i] > bestCount) { bestCount = bins[i]; best = i; }
                }
                if (best < 0 || bestCount < 6)
                {
                    // 画面偏灰时退回到整幅平均色
                    long sr = 0, sg = 0, sb = 0; int n = 0;
                    for (int y = 0; y < bmp.Height; y += step)
                        for (int x = 0; x < bmp.Width; x += step)
                        {
                            Color c = bmp.GetPixel(x, y);
                            sr += c.R; sg += c.G; sb += c.B; n++;
                        }
                    if (n == 0) return System.Drawing.Color.FromArgb(0xFF, 0x4C, 0x2B);
                    return Color.FromArgb((int)(sr / n), (int)(sg / n), (int)(sb / n));
                }
                int cnt = bins[best];
                return Color.FromArgb(rs[best] / cnt, gs[best] / cnt, bs[best] / cnt);
            }
            catch { return System.Drawing.Color.FromArgb(0xFF, 0x4C, 0x2B); }
        }
    }

    /// <summary>把 OCR 需要识别的字幕区域裁出来并放大。</summary>
    public static class SubtitleCropper
    {
        public static Bitmap Crop(Bitmap frame, int region)
        {
            try
            {
                int y0, y1;
                if (region == 0) { y0 = (int)(frame.Height * 0.62); y1 = (int)(frame.Height * 0.95); }
                else { y0 = (int)(frame.Height * 0.42); y1 = (int)(frame.Height * 0.78); }
                int x0 = (int)(frame.Width * 0.06);
                int x1 = (int)(frame.Width * 0.94);
                Rectangle rc = new Rectangle(x0, y0, Math.Max(8, x1 - x0), Math.Max(8, y1 - y0));
                Bitmap crop = new Bitmap(rc.Width, rc.Height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(crop))
                {
                    g.DrawImage(frame, new Rectangle(0, 0, crop.Width, crop.Height), rc, GraphicsUnit.Pixel);
                }
                int target = 1100;
                if (crop.Width < target)
                {
                    double r = (double)target / crop.Width;
                    int w = target, h = Math.Max(8, (int)(crop.Height * r));
                    Bitmap up = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                    using (Graphics g = Graphics.FromImage(up))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(crop, 0, 0, w, h);
                    }
                    crop.Dispose();
                    return up;
                }
                return crop;
            }
            catch { return null; }
        }
    }
}
