using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using TileMind.Common.Config;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Resource = SharpDX.DXGI.Resource;

namespace TileMind.Vision.ScreenCapture
{

    /// <summary>
    /// 基于 SharpDX 和 DXGI 桌面复制 API 的高性能屏幕捕获服务。仅支持捕获整个桌面输出，不支持按窗口捕获。适用于需要高帧率和低延迟的场景
    /// </summary>
    public class DxgiScreenCaptureService : IScreenCaptureService,IDisposable
    {
        private readonly ILogger<DxgiScreenCaptureService> _logger;

        private Factory1? _factory;
        private Adapter1? _adapter;
        private Device? _device;
        private Output? _output;
        private Output1? _output1;
        private OutputDuplication? _duplicatedOutput;
        private Texture2D? _stagingTexture;
        private bool _disposed;

        // 捕获参数
        ScreenCaptureOptions captureOptions;
        private readonly int _adapterIndex;
        private readonly int _outputIndex;

        public DxgiScreenCaptureService(ScreenCaptureOptions options, ILogger<DxgiScreenCaptureService> logger)
        {
            captureOptions = options;
            _logger = logger;
            InitializeDxgi();
        }

        private void InitializeDxgi()
        {
            try
            {
                // 创建 DXGI 工厂
                _factory = new Factory1();

                int adapterCount = _factory.GetAdapterCount();
                // 获取指定的显卡适配器 (通常是独立显卡)
                if (captureOptions.AdapterIndex >= 0 && captureOptions.AdapterIndex < adapterCount)
                {
                    _adapter = _factory.GetAdapter1(captureOptions.AdapterIndex);
                }
                else
                {
                    // 处理索引越界，例如回退到默认适配器 0
                    _adapter = _factory.GetAdapter1(0);
                }

                // 创建设备 (Device)
                _device = new Device(_adapter);

                // 获取显示器输出
                int outputCount = _adapter.GetOutputCount();
                if (captureOptions.OutputIndex >= 0 && captureOptions.OutputIndex < outputCount)
                {
                    _output = _adapter.GetOutput(captureOptions.OutputIndex);
                }
                else
                {
                    // 处理没有显示器或索引越界的情况
                    // 可回退到第一个输出，或抛出友好提示
                    if (outputCount > 0)
                        _output = _adapter.GetOutput(0);
                    else
                        throw new Exception("当前显卡未连接任何显示器，无法获取输出。");
                }
                _output1 = _output.QueryInterface<Output1>();

                // 获取输出描述，获取尺寸
                var outputDesc = _output.Description;
                int width = outputDesc.DesktopBounds.Right - outputDesc.DesktopBounds.Left;
                int height = outputDesc.DesktopBounds.Bottom - outputDesc.DesktopBounds.Top;

                // 创建用于 CPU 访问的暂存纹理 (Staging Texture)
                var textureDesc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = width,
                    Height = height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging
                };
                _stagingTexture = new Texture2D(_device, textureDesc);

                // 创建桌面复制对象
                _duplicatedOutput = _output1.DuplicateOutput(_device);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("DXGI 初始化失败。请确保显卡驱动支持 DXGI 1.2+。", ex);
            }
        }

        /// <summary>
        /// 捕获一帧屏幕图像，返回 OpenCV Mat 对象。
        /// </summary>
        /// <param name="timeoutMs">超时时间(毫秒)</param>
        /// <returns>捕获到的图像 Mat，如果失败则返回 null。</returns>
        public Mat? CaptureFrame(int timeoutMs = 10)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DxgiScreenCaptureService));
            if (_duplicatedOutput == null || _device == null || _stagingTexture == null)
                return null;

            try
            {
                OutputDuplicateFrameInformation frameInfo;
                Resource? screenResource = null;

                // 1. 尝试获取下一帧
                var result = _duplicatedOutput.TryAcquireNextFrame(timeoutMs, out frameInfo, out screenResource);

                if (result.Failure || screenResource == null)
                    return null;

                // 2. 将捕获的 GPU 资源转换为 Texture2D 并复制到暂存纹理
                using (var screenTexture = screenResource.QueryInterface<Texture2D>())
                {
                    _device.ImmediateContext.CopyResource(screenTexture, _stagingTexture);
                }

                // 3. 从暂存纹理映射数据到 CPU 内存
                var dataBox = _device.ImmediateContext.MapSubresource(
                    _stagingTexture, 0, MapMode.Read, MapFlags.None);

                // 4. 创建 OpenCV Mat 对象
                var width = _stagingTexture.Description.Width;
                var height = _stagingTexture.Description.Height;
                var mat = Mat.FromPixelData(height, width, MatType.CV_8UC4, dataBox.DataPointer);

                // 5. BGRA 格式转为 BGR (OpenCV 常用格式)
                var matBgr = new Mat();
                Cv2.CvtColor(mat, matBgr, ColorConversionCodes.BGRA2BGR);

                // 6. 解除映射并释放帧
                _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
                _duplicatedOutput.ReleaseFrame();

                screenResource.Dispose();
                return matBgr;
            }
            catch (SharpDXException ex) when (ex.ResultCode == SharpDX.DXGI.ResultCode.WaitTimeout.Result)
            {
                // 超时，无新帧可用，正常情况
                return null;
            }
            catch
            {
                // 其他错误
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _duplicatedOutput?.Dispose();
                _stagingTexture?.Dispose();
                _output1?.Dispose();
                _output?.Dispose();
                _device?.Dispose();
                _adapter?.Dispose();
                _factory?.Dispose();

                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}