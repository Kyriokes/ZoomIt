using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace Summit.Capture;

/// <summary>Bridges between Direct3D 11 objects and the Windows.Graphics.Capture WinRT types.</summary>
internal static class CaptureInterop
{
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig] int CreateForWindow(IntPtr window, ref Guid iid, out IntPtr result);
        [PreserveSig] int CreateForMonitor(IntPtr monitor, ref Guid iid, out IntPtr result);
    }

    [ComImport, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        [PreserveSig] int GetInterface(ref Guid iid, out IntPtr result);
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    public static GraphicsCaptureItem CreateItemForMonitor(IntPtr hmonitor)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var iid = GraphicsCaptureItemIid;
        Marshal.ThrowExceptionForHR(interop.CreateForMonitor(hmonitor, ref iid, out var ptr));
        try { return GraphicsCaptureItem.FromAbi(ptr); }
        finally { Marshal.Release(ptr); }
    }

    public static IDirect3DDevice CreateWinRtDevice(ID3D11Device device)
    {
        using var dxgi = device.QueryInterface<IDXGIDevice>();
        Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out var ptr));
        try { return MarshalInterface<IDirect3DDevice>.FromAbi(ptr); }
        finally { Marshal.Release(ptr); }
    }

    /// <summary>
    /// Gets the Direct3D texture behind a captured surface. Called for every frame, so it goes
    /// through raw COM pointers: nothing is left for the garbage collector to release.
    /// </summary>
    public static unsafe ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        IntPtr unknown = MarshalInterface<IDirect3DSurface>.FromManaged(surface);
        try
        {
            var accessIid = typeof(IDirect3DDxgiInterfaceAccess).GUID;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in accessIid, out IntPtr access));
            try
            {
                // IDirect3DDxgiInterfaceAccess::GetInterface is the first method after IUnknown's three.
                var getInterface = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)(*(void***)access)[3];
                var textureIid = typeof(ID3D11Texture2D).GUID;
                IntPtr texture;
                Marshal.ThrowExceptionForHR(getInterface(access, &textureIid, &texture));
                return new ID3D11Texture2D(texture);
            }
            finally { Marshal.Release(access); }
        }
        finally { Marshal.Release(unknown); }
    }
}
