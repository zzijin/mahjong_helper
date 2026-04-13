using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace TileMind.UI.Overlay.OverlayBase.DrawingCommand
{

    // ---------------------------------------------
    // EllipseCommand.cs
    public class EllipseCommand : IDrawingCommand
    {
        public Point Center { get; set; }
        public double RadiusX { get; set; }
        public double RadiusY { get; set; }

        public void Draw(DrawingContext dc, Brush fillBrush, Pen strokePen)
        {
            dc.DrawEllipse(fillBrush, strokePen, Center, RadiusX, RadiusY);
        }

        public Rect GetBounds() => new Rect(
            Center.X - RadiusX, Center.Y - RadiusY,
            RadiusX * 2, RadiusY * 2);
    }
}
