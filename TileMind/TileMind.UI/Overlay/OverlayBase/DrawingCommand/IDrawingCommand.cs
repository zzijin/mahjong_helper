using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace TileMind.UI.Overlay.OverlayBase.DrawingCommand
{
    /// <summary>
    /// 通用绘制命令接口
    /// </summary>
    public interface IDrawingCommand
    {
        /// <summary>
        /// 执行绘制操作
        /// </summary>
        /// <param name="dc">绘图上下文</param>
        /// <param name="fillBrush">填充画刷（部分命令可能忽略）</param>
        /// <param name="strokePen">描边画笔（部分命令可能忽略）</param>
        void Draw(DrawingContext dc, Brush fillBrush, Pen strokePen);

        /// <summary>
        /// 获取命令的边界矩形（用于裁剪、命中测试等，可选）
        /// </summary>
        Rect GetBounds();
    }

}
