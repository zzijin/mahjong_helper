using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace TileMind.Vision.ScreenCapture
{
    public interface IScreenCaptureService
    {
        Mat? CaptureFrame(int timeoutMs = 10);
    }
}
