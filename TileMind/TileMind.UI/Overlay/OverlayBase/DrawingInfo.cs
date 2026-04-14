using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay.OverlayBase
{
    public abstract class DrawingInfo : ITransformable
    {
        public DrawingInfo(object sourceInfo, List<IDrawingCommand> drawingCommands)
        {
            SourceInfo = sourceInfo;
            DrawingCommands = drawingCommands;
        }

        /// <summary>
        /// 所属源信息
        /// </summary>
        public virtual object SourceInfo { get; }
        /// <summary>
        /// 该结果包含的绘制命令集合（例如矩形框、文本标签等）
        /// </summary>
        public virtual List<IDrawingCommand> DrawingCommands { get; }

        /// <summary>
        /// 是否需要坐标转换
        /// </summary>
        public virtual bool NeedsTransform => false;

        /// <summary>
        /// 可选：获取该绘制区域的边界（用于裁剪、命中测试等）
        /// </summary>
        public virtual Rect GetBounds()
        {
            if (DrawingCommands.Count == 0) return Rect.Empty;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var cmd in DrawingCommands)
            {
                var bounds = cmd.GetBounds();
                if (bounds.IsEmpty) continue;

                if (bounds.Left < minX) minX = bounds.Left;
                if (bounds.Top < minY) minY = bounds.Top;
                if (bounds.Right > maxX) maxX = bounds.Right;
                if (bounds.Bottom > maxY) maxY = bounds.Bottom;
            }

            return minX == double.MaxValue ? Rect.Empty : new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not DrawingInfo other) return false;
            return ReferenceEquals(SourceInfo, other.SourceInfo);
        }
        public override int GetHashCode()
        {
            return SourceInfo.GetHashCode();
        }
    }

    public abstract class DrawingInfo<T> : DrawingInfo where T : notnull
    {
        public DrawingInfo(T sourceInfo, List<IDrawingCommand> drawingCommands) : base(sourceInfo, drawingCommands)
        {
        }

        public new T SourceInfo => (T)base.SourceInfo;
    }
}
