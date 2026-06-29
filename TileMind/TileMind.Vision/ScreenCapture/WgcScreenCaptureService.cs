// -----------------------------------------------------------------
// WgcScreenCaptureService — 基于 Windows Graphics Capture API 按窗口捕获
//
// 暂停原因：
// Windows 11 24H2 移除了 IGraphicsCaptureItemInterop COM 互操作接口。
// 替代方案 GraphicsCaptureItem.CreateFromWindowId(WindowId) 需要
// Microsoft.WindowsAppSDK NuGet 包（400MB+），引入成本过高。
//
// 后续可选方案：
// 1. GraphicsCapturePicker（系统窗口选择器，需用户交互）
// 2. 评估 WindowsAppSDK 依赖体积是否可接受后启用
// 3. DXGI + 覆盖层遮罩（截取后填充已知覆盖层坐标区域）
// -----------------------------------------------------------------

//using System.Diagnostics;
//using System.Runtime.InteropServices;
//using Microsoft.Extensions.Logging;
//using OpenCvSharp;
//using TileMind.Common.Config;
//using Windows.Graphics.Capture;
//using Windows.Graphics.DirectX;
//using Windows.Graphics.DirectX.Direct3D11;
//using SharpDX.Direct3D11;
//using SharpDX.DXGI;
//using Device = SharpDX.Direct3D11.Device;
//
//namespace TileMind.Vision.ScreenCapture;
//
//public class WgcScreenCaptureService : IScreenCaptureService, IDisposable
//{
//    ...
//}
