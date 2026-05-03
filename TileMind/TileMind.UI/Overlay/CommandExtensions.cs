using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.UI.Overlay
{
    internal static class CommandExtensions
    {
        extension(System.Windows.Rect wrect)
        {
            public OpenCvSharp.Rect ToMRect()
            {
                return new OpenCvSharp.Rect((int)wrect.X, (int)wrect.Y, (int)wrect.Width, (int)wrect.Height);
            }
        }

        extension(OpenCvSharp.Rect rect)
        {
            public System.Windows.Rect ToWRect()
            {
                return new System.Windows.Rect(rect.X, rect.Y, rect.Width, rect.Height);
            }
        }
    }
}
