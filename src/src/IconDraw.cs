using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Rap.Studio
{
    /// <summary>程序图标：橙红渐变圆角方块 + 白色麦克风。界面与窗口图标共用同一套矢量绘制。</</summary>
    public static class IconDraw
    {
        public static UIElement Logo(double size)
        {
            Border b = new Border();
            b.Width = size;
            b.Height = size;
            b.CornerRadius = new CornerRadius(size * 0.26);
            LinearGradientBrush bg = new LinearGradientBrush();
            bg.StartPoint = new Point(0, 0);
            bg.EndPoint = new Point(1, 1);
            bg.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0x7A, 0x45), 0));
            bg.GradientStops.Add(new GradientStop(Color.FromRgb(0xE2, 0x3B, 0x14), 1));
            b.Background = bg;

            Path p = new Path();
            p.Data = Icons.Mic();
            p.Stretch = Stretch.Uniform;
            p.Width = size * 0.56;
            p.Height = size * 0.56;
            p.Stroke = new SolidColorBrush(Colors.White);
            p.StrokeThickness = 2.2;
            p.StrokeLineJoin = PenLineJoin.Round;

            Grid g = new Grid();
            Border avatar = b;
            g.Children.Add(avatar);
            g.Children.Add(p);
            return g;
        }

        public static BitmapSource IconSource(int px)
        {
            DrawingVisual v = new DrawingVisual();
            using (DrawingContext dc = v.RenderOpen())
            {
                double s = px / 18.0;
                dc.PushTransform(new ScaleTransform(s, s));
                DrawingBrush bgBrush = null;
                LinearGradientBrush bg = new LinearGradientBrush();
                bg.StartPoint = new Point(0, 0);
                bg.EndPoint = new Point(1, 1);
                bg.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0x7A, 0x45), 0));
                bg.GradientStops.Add(new GradientStop(Color.FromRgb(0xE2, 0x3B, 0x14), 1));
                RectangleGeometry rg = new RectangleGeometry(new Rect(0.6, 0.6, 16.8, 16.8), 4.2, 4.2);
                dc.DrawGeometry(bg, null, rg);

                Geometry mic = Icons.Mic();
                Pen pen = new Pen(new SolidColorBrush(Colors.White), 1.35);
                pen.LineJoin = PenLineJoin.Round;
                dc.DrawGeometry(null, pen, mic);
                dc.Pop();
            }
            RenderTargetBitmap bmp = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(v);
            return bmp;
        }
    }
}
