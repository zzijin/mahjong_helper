using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace TileMind.UI.Overlay.OverlayBase.DrawingCommand
{

    // ---------------------------------------------
    // PolygonCommand.cs
    public record class PolygonCommand : IDrawingCommand
    {
        public PointCollection Points { get; set; } = new PointCollection();
        public bool IsClosed { get; set; } = true;
        public bool IsFilled { get; set; } = true;

        public void Draw(DrawingContext dc, Brush fillBrush, Pen strokePen)
        {
            if (Points.Count < 2) return;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(Points[0], IsFilled, IsClosed);
                var remaining = Points.Skip(1).ToList();
                ctx.PolyLineTo(remaining, IsFilled, true);
            }
            dc.DrawGeometry(IsFilled ? fillBrush : null, strokePen, geometry);
        }

        public Rect GetBounds()
        {
            if (Points.Count == 0) return Rect.Empty;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
