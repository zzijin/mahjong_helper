using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.UI.Overlay.OverlayBase.DrawingCommand
{
    /// <summary>
    /// 组合生成器，将多个生成器的结果合并
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public class CompositeDrawingCommandGenerator<TData> : IDrawingCommandGenerator<TData>
    {
        private readonly List<IDrawingCommandGenerator<TData>> _generators = new();

        public void AddGenerator(IDrawingCommandGenerator<TData> generator)
        {
            _generators.Add(generator);
        }

        public IEnumerable<IDrawingCommand> GenerateCommands(TData data)
        {
            return _generators.SelectMany(g => g.GenerateCommands(data));
        }
    }
}
