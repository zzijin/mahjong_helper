using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using TileMind.UI.Overlay.OverlayBase;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay
{
    internal class MahjongOverlayControl : OverlayBaseControl
    {

        protected override (Brush fillBrush, Pen strokePen) GetDrawingStyles(DrawingInfo info)
        {
            return info switch
            {
                MahjongTileDrawingInfo tileInfo => GetTileStyles(tileInfo),
                _ => (Brushes.Transparent, new Pen(Brushes.Transparent, 0))
            };
        }

        private (Brush fillBrush, Pen strokePen) GetTileStyles(MahjongTileDrawingInfo drawingInfo)
        {
            var tile = drawingInfo.SourceInfo;
            Color color = Colors.LimeGreen ;
            Brush fill = new SolidColorBrush(Color.FromArgb((byte)(FillOpacity * 255), color.R, color.G, color.B));
            Pen pen = new Pen(new SolidColorBrush(color), StrokeThickness);
            return (fill, pen);
        }
    }
}
