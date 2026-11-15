using System.Diagnostics;
using System.Runtime.InteropServices;
using Summit.Core;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace Summit.Capture;

/// <summary>
/// Captures a monitor and draws a zoomed region of it into a window. Frames stay on the GPU:
/// the captured texture is sampled directly by a full-screen triangle, and nothing is drawn
/// unless a new frame arrives or the view changes.
/// </summary>
internal sealed class Mirror : IDisposable
{
    private const string ShaderSource = """
        cbuffer View : register(b0) { float2 uvOffset; float2 uvScale; };
        Texture2D tex : register(t0);
        SamplerState smp : register(s0);
        struct VSOut { float4 pos : SV_Position; float2 uv : TEXCOORD0; };
        VSOut VS(uint id : SV_VertexID)
        {
            VSOut o;
            float2 t = float2((id << 1) & 2, id & 2);
            o.pos = float4(t * float2(2, -2) + float2(-1, 1), 0, 1);
            o.uv = uvOffset + t * uvScale;
            return o;
        }
        float4 PS(VSOut i) : SV_Target { return float4(tex.Sample(smp, i.uv).rgb, 1); }
        """;

    private const DirectXPixelFormat PixelFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
    private const int PoolBuffers = 2;

    private static readonly bool HasMinUpdateInterval = ApiInformation.IsPropertyPresent(
        "Windows.Graphics.Capture.GraphicsCaptureSession", "MinUpdateInterval");
    private static readonly bool HasBorderToggle = ApiInformation.IsPropertyPresent(
        "Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired");

    [StructLayout(LayoutKind.Sequential)]
    private struct ViewConstants { public float OffsetX, OffsetY, ScaleX, ScaleY; }

    private readonly object _sync = new();
    private readonly IntPtr _hwnd;
    private readonly FrameLimiter _limiter = new(Stopwatch.Frequency);

    // Device resources.
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGISwapChain1? _swapChain;
    private ID3D11RenderTargetView? _target;
    private ID3D11VertexShader? _vs;
    private ID3D11PixelShader? _ps;
    private ID3D11SamplerState? _sampler;
    private ID3D11Buffer? _constants;
    private IDirect3DDevice? _winrtDevice;

    // Capture session.
    private IntPtr _monitor;
    private Direct3D11CaptureFramePool? _pool;
    private GraphicsCaptureSession? _session;
    private SizeInt32 _poolSize;

    // The most recent frame, kept so the view can be redrawn while the source is static.
    private Direct3D11CaptureFrame? _frame;
    private ID3D11Texture2D? _frameTexture;
    private ID3D11ShaderResourceView? _frameView;

    private int _width, _height;
    private float _zoom = 1f, _anchorX = 0.5f, _anchorY = 0.5f;
    private int _fps;
    private int _framesShown;
    private bool _disposed;

    public static bool IsSupported => GraphicsCaptureSession.IsSupported();

    /// <summary>Total number of captured frames drawn so far.</summary>
    public int FramesShown => Volatile.Read(ref _framesShown);

    public Mirror(IntPtr viewHwnd, int width, int height)
    {
        _hwnd = viewHwnd;
        _width = width;
        _height = height;
        CreateDeviceResources();
    }

    public void SetMonitor(IntPtr hmonitor)
    {
        lock (_sync)
        {
            if (_disposed) return;
            _monitor = hmonitor;
            Guard(StartCapture);
        }
    }

    public void SetView(float zoom, float anchorX, float anchorY)
    {
        lock (_sync)
        {
            _zoom = zoom;
            _anchorX = anchorX;
            _anchorY = anchorY;
            Guard(Render);
        }
    }

    /// <param name="fps">Frames per second; 0 means no limit.</param>
    public void SetFps(int fps)
    {
        lock (_sync)
        {
            _fps = Math.Max(0, fps);
            ApplyFps();
        }
    }

    public void Resize(int width, int height)
    {
        lock (_sync)
        {
            _width = width;
            _height = height;
            if (_disposed || width <= 0 || height <= 0) return;
            Guard(() =>
            {
                ResizeSwapChain();
                Render();
            });
        }
    }

    public void Redraw()
    {
        lock (_sync) Guard(Render);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            StopCapture();
            ReleaseDeviceResources();
        }
    }

    // ---- Device ----

    private void CreateDeviceResources()
    {
        D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_0],
            out _device, out _context).CheckError();

        using (var dxgiDevice = _device!.QueryInterface<IDXGIDevice>())
        using (var adapter = dxgiDevice.GetAdapter())
        using (var factory = adapter.GetParent<IDXGIFactory2>())
        {
            var desc = new SwapChainDescription1
            {
                Width = (uint)Math.Max(1, _width),
                Height = (uint)Math.Max(1, _height),
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipDiscard,
                AlphaMode = AlphaMode.Ignore,
            };
            _swapChain = factory.CreateSwapChainForHwnd(_device, _hwnd, desc);
            factory.MakeWindowAssociation(_hwnd, WindowAssociationFlags.IgnoreAll);
        }
        CreateTarget();

        _vs = _device.CreateVertexShader(Compiler.Compile(ShaderSource, "VS", "Summit", "vs_4_0").Span);
        _ps = _device.CreatePixelShader(Compiler.Compile(ShaderSource, "PS", "Summit", "ps_4_0").Span);
        _sampler = _device.CreateSamplerState(SamplerDescription.LinearClamp);
        _constants = _device.CreateBuffer(new BufferDescription(
            (uint)Marshal.SizeOf<ViewConstants>(), BindFlags.ConstantBuffer));
        _winrtDevice = CaptureInterop.CreateWinRtDevice(_device);
    }

    private void CreateTarget()
    {
        using var backBuffer = _swapChain!.GetBuffer<ID3D11Texture2D>(0);
        _target = _device!.CreateRenderTargetView(backBuffer);
    }

    private void ResizeSwapChain()
    {
        if (_swapChain is null) return;
        _context!.ClearState();
        _target?.Dispose();
        _target = null;
        _swapChain.ResizeBuffers(0, (uint)_width, (uint)_height, Format.Unknown, SwapChainFlags.None).CheckError();
        CreateTarget();
    }

    private void ReleaseDeviceResources()
    {
        _winrtDevice?.Dispose(); _winrtDevice = null;
        _constants?.Dispose(); _constants = null;
        _sampler?.Dispose(); _sampler = null;
        _ps?.Dispose(); _ps = null;
        _vs?.Dispose(); _vs = null;
        _target?.Dispose(); _target = null;
        _swapChain?.Dispose(); _swapChain = null;
        _context?.Dispose(); _context = null;
        _device?.Dispose(); _device = null;
    }

    // ---- Capture ----

    private void StartCapture()
    {
        StopCapture();
        if (_monitor == IntPtr.Zero || _winrtDevice is null) return;

        var item = CaptureInterop.CreateItemForMonitor(_monitor);
        _poolSize = item.Size;
        _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_winrtDevice, PixelFormat, PoolBuffers, _poolSize);
        _pool.FrameArrived += OnFrameArrived;
        _session = _pool.CreateCaptureSession(item);
        _session.IsCursorCaptureEnabled = true;
        if (HasBorderToggle)
        {
            try { _session.IsBorderRequired = false; }
            catch (Exception) { /* The border is cosmetic; keep going with it visible. */ }
        }
        ApplyFps();
        _session.StartCapture();
    }

    private void StopCapture()
    {
        if (_pool is not null) _pool.FrameArrived -= OnFrameArrived;
        _session?.Dispose(); _session = null;
        ReleaseFrame();
        _pool?.Dispose(); _pool = null;
    }

    private void ReleaseFrame()
    {
        _frameView?.Dispose(); _frameView = null;
        _frameTexture?.Dispose(); _frameTexture = null;
        _frame?.Dispose(); _frame = null;
    }

    private void ApplyFps()
    {
        // The limiter sets the pace by dropping frames that arrive too early. Where the system
        // can also throttle the capture itself, ask it for roughly twice the target rate: that
        // saves most of the capture work while leaving the limiter enough frames to pick from
        // (the system's own pacing is too coarse to hit a target rate on its own).
        _limiter.Fps = _fps;
        if (!HasMinUpdateInterval || _session is null) return;
        try
        {
            _session.MinUpdateInterval = _fps > 0
                ? TimeSpan.FromSeconds(0.5 / _fps)
                : TimeSpan.FromMilliseconds(1);
        }
        catch (Exception)
        {
            // Not adjustable on this system; the limiter alone still holds the rate.
        }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        lock (_sync)
        {
            if (_disposed || sender != _pool) return;
            try
            {
                Guard(() => TakeFrame(sender));
            }
            catch (Exception)
            {
                // This runs on a capture thread, where an unhandled error would end the app.
                // Drop the frame instead; the next one gets a fresh attempt.
                ReleaseFrame();
            }
        }
    }

    private void TakeFrame(Direct3D11CaptureFramePool sender)
    {
        // Always show the newest frame available.
        var frame = sender.TryGetNextFrame();
        if (frame is null) return;
        while (sender.TryGetNextFrame() is { } newer)
        {
            frame.Dispose();
            frame = newer;
        }

        if (!_limiter.ShouldRender(Stopwatch.GetTimestamp()))
        {
            frame.Dispose();
            return;
        }

        var size = frame.ContentSize;
        if (size.Width != _poolSize.Width || size.Height != _poolSize.Height)
        {
            // The monitor changed resolution: resize the pool and wait for the next frame.
            frame.Dispose();
            ReleaseFrame();
            if (size.Width > 0 && size.Height > 0)
            {
                _poolSize = size;
                sender.Recreate(_winrtDevice, PixelFormat, PoolBuffers, size);
            }
            return;
        }

        ReleaseFrame();
        _frame = frame;
        using (var surface = frame.Surface)
            _frameTexture = CaptureInterop.GetTexture(surface);
        _frameView = _device!.CreateShaderResourceView(_frameTexture);
        Render();
        Interlocked.Increment(ref _framesShown);
    }

    // ---- Drawing ----

    private void Render()
    {
        if (_disposed || _context is null || _target is null || _swapChain is null) return;
        if (_width <= 0 || _height <= 0) return;

        _context.OMSetRenderTargets(_target);
        _context.ClearRenderTargetView(_target, new Color4(0f, 0f, 0f, 1f));

        var (dest, src) = ViewMath.Fit(_poolSize.Width, _poolSize.Height, _width, _height, _zoom, _anchorX, _anchorY);
        if (_frameView is not null && dest.Width > 0 && dest.Height > 0)
        {
            var constants = new ViewConstants { OffsetX = src.X, OffsetY = src.Y, ScaleX = src.Width, ScaleY = src.Height };
            _context.UpdateSubresource(in constants, _constants!);

            _context.RSSetViewport(dest.X, dest.Y, dest.Width, dest.Height);
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            _context.VSSetShader(_vs);
            _context.VSSetConstantBuffer(0, _constants);
            _context.PSSetShader(_ps);
            _context.PSSetConstantBuffer(0, _constants);
            _context.PSSetSampler(0, _sampler);
            _context.PSSetShaderResource(0, _frameView);
            _context.Draw(3, 0);
        }

        _swapChain.Present(0, PresentFlags.None).CheckError();
    }

    /// <summary>
    /// Runs a graphics operation; if the device was lost (driver update, GPU reset), rebuilds
    /// everything and carries on. If rebuilding fails, the next operation tries again.
    /// </summary>
    private void Guard(Action action)
    {
        if (_disposed) return;
        if (_device is null && !TryRecover()) return;
        try
        {
            action();
        }
        catch (Exception e) when (IsDeviceLoss(e))
        {
            TryRecover();
        }
    }

    private bool TryRecover()
    {
        StopCapture();
        ReleaseDeviceResources();
        try
        {
            CreateDeviceResources();
            StartCapture();
            return true;
        }
        catch (Exception)
        {
            // Still unavailable (GPU mid-reset): leave everything released so the next call retries.
            StopCapture();
            ReleaseDeviceResources();
            return false;
        }
    }

    private static bool IsDeviceLoss(Exception e)
    {
        const int DeviceRemoved = unchecked((int)0x887A0005);
        const int DeviceReset = unchecked((int)0x887A0007);
        const int DeviceHung = unchecked((int)0x887A0006);
        return e.HResult is DeviceRemoved or DeviceReset or DeviceHung;
    }
}
