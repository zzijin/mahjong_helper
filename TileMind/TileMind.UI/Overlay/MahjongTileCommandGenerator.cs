using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;
using TileMind.Vision.Detection;

namespace TileMind.UI.Overlay
{
    public class MahjongTileCommandGenerator : IDrawingCommandGenerator<DetectionResult>
    {
        public IEnumerable<IDrawingCommand> GenerateCommands(DetectionResult tile)
        {
            // 1. 绘制牌的外框
            yield return new RectangleCommand
            {
                Rect = tile.BoundingBox,
                CornerRadius = 4
            };

            // 2. 绘制牌名和置信度文本
            string label = $"{tile.TileName} ({tile.Confidence:P0})";
            Point textPos = new Point(tile.BoundingBox.Left, tile.BoundingBox.Top - 5);
            yield return new TextCommand
            {
                Text = label,
                Position = textPos,
                FontSize = 12,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(200, 40, 40, 40))
            };
        }
    }
}
