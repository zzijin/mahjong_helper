using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace TileMind.UI.Overlay.OverlayBase.DrawingCommand
{


    // ---------------------------------------------
    // RectangleCommand.cs
    public class RectangleCommand : IDrawingCommand
    {
        public Rect Rect { get; set; }
        public double CornerRadius { get; set; } = 0;

        public void Draw(DrawingContext dc, Brush fillBrush, Pen strokePen)
        {
            if (CornerRadius > 0)
                dc.DrawRoundedRectangle(fillBrush, strokePen, Rect, CornerRadius, CornerRadius);
            else
                dc.DrawRectangle(fillBrush, strokePen, Rect);
        }

        public Rect GetBounds() => Rect;
    }
}
