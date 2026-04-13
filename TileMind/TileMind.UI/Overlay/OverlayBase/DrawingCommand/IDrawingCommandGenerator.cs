using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.UI.Overlay.OverlayBase.DrawingCommand
{
    /// <summary>
    /// 绘制命令生成器接口：将任意数据模型转换为绘制命令集合
    /// </summary>
    /// <typeparam name="TData">输入数据模型类型</typeparam>
    public interface IDrawingCommandGenerator<in TData>
    {
        IEnumerable<IDrawingCommand> GenerateCommands(TData data);
    }
}
