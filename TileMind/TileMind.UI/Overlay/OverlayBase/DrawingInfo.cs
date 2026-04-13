using System;
using System.Collections.Generic;
using System.Text;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay.OverlayBase
{
    public abstract class DrawingInfo
    {
        public DrawingInfo(object sourceInfo, List<IDrawingCommand> drawingCommands)
        {
            SrouceInfo = sourceInfo;
            DrawingCommands = drawingCommands;
        }

        /// <summary>
        /// 源信息
        /// </summary>
        public object SrouceInfo { get; }
        /// <summary>
        /// 该结果包含的绘制命令集合（例如矩形框、文本标签等）
        /// </summary>
        public List<IDrawingCommand> DrawingCommands { get; }

        public override bool Equals(object? obj)
        {
            if (obj is not DrawingInfo other) return false;
            return ReferenceEquals(SrouceInfo, other.SrouceInfo);
        }
        public override int GetHashCode()
        {
            return SrouceInfo.GetHashCode();
        }
    }
}
