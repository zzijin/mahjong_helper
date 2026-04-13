using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace TileMind.UI.Overlay.OverlayBase.DrawingCommand
{

    // ---------------------------------------------
    // TextCommand.cs
    public class TextCommand : IDrawingCommand
    {
        public string Text { get; set; } = "";
        public Point Position { get; set; }                // 文本基线起点
        public double FontSize { get; set; } = 12;
        public Brush Foreground { get; set; } = Brushes.White;
        public Brush Background { get; set; } = new SolidColorBrush(Color.FromArgb(200, 40, 40, 40));
        public Typeface Typeface { get; set; } = new Typeface("Consolas");
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;
        public bool DrawBackground { get; set; } = true;

        // 用于DPI感知的文本格式化
        private FormattedText CreateFormattedText(double pixelsPerDip)
        {
            return new FormattedText(
                Text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface,
                FontSize,
                Foreground,
                pixelsPerDip)
            {
                TextAlignment = Alignment
            };
        }

        public void Draw(DrawingContext dc, Brush fillBrush, Pen strokePen)
        {
            // 获取DPI缩放因子（需要从当前可视化树获取，这里使用静态方法从主窗口获取）
            double pixelsPerDip = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip;
            var formatted = CreateFormattedText(pixelsPerDip);

            // 根据对齐方式调整绘制位置
            double offsetX = 0;
            if (Alignment == TextAlignment.Center)
                offsetX = -formatted.Width / 2;
            else if (Alignment == TextAlignment.Right)
                offsetX = -formatted.Width;

            Point drawPos = new Point(Position.X + offsetX, Position.Y);

            // 绘制背景
            if (DrawBackground)
            {
                double padding = 4;
                Rect bgRect = new Rect(
                    drawPos.X - padding,
                    drawPos.Y - formatted.Height - padding,
                    formatted.Width + 2 * padding,
                    formatted.Height + 2 * padding);
                dc.DrawRectangle(Background, null, bgRect);
            }

            dc.DrawText(formatted, drawPos);
        }

        public Rect GetBounds()
        {
            // 简化实现，实际可在Draw时缓存精确矩形
            return Rect.Empty;
        }
    }
}
