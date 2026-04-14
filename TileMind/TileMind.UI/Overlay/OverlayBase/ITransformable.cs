using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.UI.Overlay.OverlayBase
{
    /// <summary>
    /// 标记该 DrawingInfo 是否需要应用坐标变换
    /// </summary>
    public interface ITransformable
    {
        bool NeedsTransform { get; }
    }
}
