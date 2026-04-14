using System;
using System.Collections.Generic;
using System.Text;
using TileMind.Common.Models;
using TileMind.UI.Overlay.OverlayBase;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay
{
    internal class MahjongTileDrawingInfo : DrawingInfo<IList<TileDetectionResult>>
    {
        public MahjongTileDrawingInfo(IList<TileDetectionResult> data, List<IDrawingCommand> drawingCommands) : base(data, drawingCommands)
        {

        }
    }
}
