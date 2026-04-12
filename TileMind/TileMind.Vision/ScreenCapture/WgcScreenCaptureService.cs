//using OpenCvSharp;
//using System;
//using System.Diagnostics;
//using System.Drawing;
//using System.Runtime.InteropServices;
//using Windows.Graphics.Capture;
//using Windows.Graphics.DirectX.Direct3D11;
//using Windows.Foundation;
//using SharpDX.Direct3D11;
//using Device = SharpDX.Direct3D11.Device;
//using System.Threading.Tasks;

//Microsoft.Windows.SDK.Contracts不支持.NET5以上
namespace TileMind.Vision.ScreenCapture
{
    //部分函数已过时，需要检查函数，暂不使用
    ///// <summary>
    ///// 基于 Windows Graphics Capture API 的屏幕捕获服务，支持按窗口捕获。
    ///// </summary>
    //public class WgcScreenCaptureService : IDisposable
    //{
    //    private GraphicsCaptureItem? _captureItem;
    //    private Direct3D11CaptureFramePool? _framePool;
    //    private GraphicsCaptureSession? _captureSession;
    //    private Device? _device;
    //    private Texture2D? _stagingTexture;
    //    private TaskCompletionSource<Mat?>? _frameTaskSource;
    //    private bool _disposed;

    //    public WgcScreenCaptureService(IntPtr hWnd)
    //    {
    //        InitializeWgc(hWnd);
    //    }

    //    private void InitializeWgc(IntPtr hWnd)
    //    {
    //        try
    //        {
    //            // 1. 创建捕获项 (按窗口句柄)
    //            _captureItem = GraphicsCaptureItem.TryCreateFromWindowId(hWnd);
    //            if (_captureItem == null)
    //                throw new InvalidOperationException("无法从指定窗口创建捕获项。");

    //            // 2. 创建 D3D11 设备
    //            _device = new Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport);

    //            // 3. 将 SharpDX 设备转换为 WinRT 的 IDirect3DDevice
    //            var dxgiDevice = _device.QueryInterface<SharpDX.DXGI.Device>();
    //            var winrtDevice = Direct3D11Device.FromDirect3D11Device(dxgiDevice);

    //            // 4. 创建帧池，指定像素格式
    //            _framePool = Direct3D11CaptureFramePool.Create(
    //                winrtDevice,
    //                DirectXPixelFormat.B8G8R8A8UIntNormalized,
    //                2, // 缓冲帧数
    //                _captureItem.Size);

    //            // 5. 当新帧到达时，处理帧数据
    //            _framePool.FrameArrived += OnFrameArrived;

    //            // 6. 创建并启动捕获会话
    //            _captureSession = _framePool.CreateCaptureSession(_captureItem);
    //            _captureSession.StartCapture();
    //        }
    //        catch (Exception ex)
    //        {
    //            throw new InvalidOperationException("WGC 初始化失败。请确保系统为 Windows 10 1903 或更高版本。", ex);
    //        }
    //    }

    //    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    //    {
    //        if (_frameTaskSource == null) return;

    //        using (var frame = sender.TryGetNextFrame())
    //        {
    //            if (frame == null) return;

    //            // 获取 D3D11 纹理
    //            var surface = frame.Surface;
    //            var dxgiSurface = surface.QueryInterface<SharpDX.DXGI.Surface>();
    //            var texture = dxgiSurface.QueryInterface<Texture2D>();

    //            // 将纹理数据复制到 CPU 可访问的暂存纹理
    //            if (_stagingTexture == null || _stagingTexture.Description.Width != texture.Description.Width || _stagingTexture.Description.Height != texture.Description.Height)
    //            {
    //                _stagingTexture?.Dispose();
    //                var desc = new Texture2DDescription
    //                {
    //                    CpuAccessFlags = CpuAccessFlags.Read,
    //                    BindFlags = BindFlags.None,
    //                    Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
    //                    Width = texture.Description.Width,
    //                    Height = texture.Description.Height,
    //                    MipLevels = 1,
    //                    ArraySize = 1,
    //                    SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
    //                    Usage = ResourceUsage.Staging
    //                };
    //                _stagingTexture = new Texture2D(_device, desc);
    //            }

    //            _device.ImmediateContext.CopyResource(texture, _stagingTexture);

    //            // 映射数据到 CPU 内存并转换为 Mat
    //            var dataBox = _device.ImmediateContext.MapSubresource(_stagingTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
    //            var mat = new Mat(texture.Description.Height, texture.Description.Width, MatType.CV_8UC4, dataBox.DataPointer);
    //            var matBgr = new Mat();
    //            Cv2.CvtColor(mat, matBgr, ColorConversionCodes.BGRA2BGR);
    //            _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);

    //            _frameTaskSource.SetResult(matBgr);
    //        }
    //    }

    //    /// <summary>
    //    /// 异步捕获一帧图像。
    //    /// </summary>
    //    public Task<Mat?> CaptureFrameAsync()
    //    {
    //        if (_disposed) throw new ObjectDisposedException(nameof(WgcScreenCaptureService));

    //        _frameTaskSource = new TaskCompletionSource<Mat?>();
    //        return _frameTaskSource.Task;
    //    }

    //    public void Dispose()
    //    {
    //        if (!_disposed)
    //        {
    //            _captureSession?.Dispose();
    //            _framePool?.Dispose();
    //            _stagingTexture?.Dispose();
    //            _device?.Dispose();
    //            _disposed = true;
    //        }
    //        GC.SuppressFinalize(this);
    //    }
    //}
}