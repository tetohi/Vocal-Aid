using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rap.Studio
{
    /// <summary>配色与字体：参考 PCL2 的简洁浅色卡片风格，主色为说唱感的橙红。</summary>
    public static class Theme
    {
        public static readonly Color AccentColor = Color.FromRgb(0xFF, 0x4C, 0x2B);
        public static readonly Color AccentDeepColor = Color.FromRgb(0xD9, 0x3A, 0x1A);
        public static readonly Color AccentSoftColor = Color.FromRgb(0xFF, 0xEE, 0xE9);
        public static readonly Color BgColor = Color.FromRgb(0xF4, 0xF5, 0xF7);
        public static readonly Color NavColor = Color.FromRgb(0xFF, 0xFF, 0xFF);
        public static readonly Color CardColor = Color.FromRgb(0xFF, 0xFF, 0xFF);
        public static readonly Color BorderColor = Color.FromRgb(0xE5, 0xE7, 0xEB);
        public static readonly Color TextColor = Color.FromRgb(0x17, 0x1A, 0x20);
        public static readonly Color SubColor = Color.FromRgb(0x6E, 0x76, 0x81);
        public static readonly Color MuteColor = Color.FromRgb(0x9A, 0xA1, 0xAB);
        public static readonly Color SuccessColor = Color.FromRgb(0x14, 0xA3, 0x5A);
        public static readonly Color WarnColor = Color.FromRgb(0xF2, 0x8B, 0x00);
        public static readonly Color DangerColor = Color.FromRgb(0xE5, 0x3E, 0x3E);
        public static readonly Color DarkColor = Color.FromRgb(0x11, 0x13, 0x18);

        public static readonly SolidColorBrush Accent = Freeze(new SolidColorBrush(AccentColor));
        public static readonly SolidColorBrush AccentDeep = Freeze(new SolidColorBrush(AccentDeepColor));
        public static readonly SolidColorBrush AccentSoft = Freeze(new SolidColorBrush(AccentSoftColor));
        public static readonly SolidColorBrush Bg = Freeze(new SolidColorBrush(BgColor));
        public static readonly SolidColorBrush Nav = Freeze(new SolidColorBrush(NavColor));
        public static readonly SolidColorBrush Card = Freeze(new SolidColorBrush(CardColor));
        public static readonly SolidColorBrush Border = Freeze(new SolidColorBrush(BorderColor));
        public static readonly SolidColorBrush Text = Freeze(new SolidColorBrush(TextColor));
        public static readonly SolidColorBrush Sub = Freeze(new SolidColorBrush(SubColor));
        public static readonly SolidColorBrush Mute = Freeze(new SolidColorBrush(MuteColor));
        public static readonly SolidColorBrush Success = Freeze(new SolidColorBrush(SuccessColor));
        public static readonly SolidColorBrush Warn = Freeze(new SolidColorBrush(WarnColor));
        public static readonly SolidColorBrush Danger = Freeze(new SolidColorBrush(DangerColor));
        public static readonly SolidColorBrush Dark = Freeze(new SolidColorBrush(DarkColor));
        public static readonly SolidColorBrush Transparent = Freeze(new SolidColorBrush(Colors.Transparent));

        public static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI, Microsoft YaHei, Segoe UI");

        public static SolidColorBrush Muted()
        {
            return Mute;
        }

        public static FontWeight Bold = FontWeights.SemiBold;
        public static FontWeight Heavy = FontWeights.Bold;
        public static FontWeight Normal = FontWeights.Normal;

        public static readonly CubicEase EaseOut = FreezeEase(EasingMode.EaseOut, 0.15);
        public static readonly CubicEase EaseInOut = FreezeEase(EasingMode.EaseInOut, 0.35);
        public static readonly IEasingFunction Smooth = FreezeEase(EasingMode.EaseOut, 0.2);

        private static CubicEase FreezeEase(EasingMode mode, double power)
        {
            CubicEase e = new CubicEase();
            e.EasingMode = mode;
            e.Freeze();
            return e;
        }

        private static SolidColorBrush Freeze(SolidColorBrush b)
        {
            b.Freeze();
            return b;
        }

        public static SolidColorBrush New(Color c)
        {
            return new SolidColorBrush(c);
        }

        public static SolidColorBrush New(Color c, byte alpha)
        {
            return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        }

        public static Color Lerp(Color a, Color b, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(
                (byte)(a.A + (b.A - a.A) * t),
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        public static Color Darken(Color c, double amount)
        {
            return Lerp(c, Colors.Black, amount);
        }

        public static Color Lighten(Color c, double amount)
        {
            return Lerp(c, Colors.White, amount);
        }

        /// <summary>把颜色调整为适合做卡的柔和底色（保留色相，降低饱和/提高亮度）。</summary>
        public static Color Soften(Color c)
        {
            return Lerp(c, Colors.White, 0.55);
        }

        /// <summary>渐变下端的提亮版本（保持同一色相，只调明度）。</summary>
        public static Color Soften2(Color c)
        {
            return Lerp(c, Colors.White, 0.38);
        }
    }
}
