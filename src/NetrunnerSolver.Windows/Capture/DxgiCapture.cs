using System.Runtime.InteropServices;
using NetrunnerSolver.Core.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace NetrunnerSolver.Windows.Capture;

public static class DxgiCapture
{
    private const uint AcquireTimeoutMs = 100;
    private const int AcquireAttempts = 5;

    public static BgraImage Capture(MonitorArea area)
    {
        ArgumentNullException.ThrowIfNull(area);
        DXGI.CreateDXGIFactory1(out IDXGIFactory1? factory).CheckError();
        using (factory)
        {
            for (uint a = 0; factory!.EnumAdapters1(a, out var adapter).Success; a++)
            {
                using (adapter)
                {
                    for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
                    {
                        using (output)
                        {
                            if (output.Description.Monitor == area.Handle)
                            {
                                return CaptureOutput(adapter, output);
                            }
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException("No DXGI output matches the monitor.");
    }

    private static BgraImage CaptureOutput(IDXGIAdapter1 adapter, IDXGIOutput output)
    {
        var rotation = output.Description.Rotation;
        if (rotation is not ModeRotation.Identity and not ModeRotation.Unspecified)
        {
            throw new NotSupportedException($"Rotated output ({rotation}) is not supported.");
        }

        D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport, [FeatureLevel.Level_11_0, FeatureLevel.Level_10_0], out ID3D11Device? device, out ID3D11DeviceContext? context).CheckError();
        using (device)
        using (context)
        using (var output1 = output.QueryInterface<IDXGIOutput1>())
        using (var duplication = output1.DuplicateOutput(device))
        {
            for (var attempt = 0; attempt < AcquireAttempts; attempt++)
            {
                var result = duplication.AcquireNextFrame(AcquireTimeoutMs, out _, out var resource);
                if (result == Vortice.DXGI.ResultCode.WaitTimeout)
                {
                    continue;
                }

                result.CheckError();
                try
                {
                    using (resource)
                    using (var texture = resource!.QueryInterface<ID3D11Texture2D>())
                    {
                        return Read(device!, context!, texture);
                    }
                }
                finally
                {
                    duplication.ReleaseFrame();
                }
            }
        }

        throw new TimeoutException("Desktop duplication returned no frame.");
    }

    private static BgraImage Read(ID3D11Device device, ID3D11DeviceContext context, ID3D11Texture2D texture)
    {
        var description = texture.Description;
        description.CPUAccessFlags = CpuAccessFlags.Read;
        description.Usage = ResourceUsage.Staging;
        description.BindFlags = BindFlags.None;
        description.MiscFlags = ResourceOptionFlags.None;
        description.MipLevels = 1;
        description.ArraySize = 1;
        using var staging = device.CreateTexture2D(description);
        context.CopyResource(staging, texture);
        var mapped = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            var width = (int)description.Width;
            var height = (int)description.Height;
            var pixels = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(mapped.DataPointer + (y * (int)mapped.RowPitch), pixels, y * width * 4, width * 4);
            }

            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }

            return new BgraImage(width, height, pixels);
        }
        finally
        {
            context.Unmap(staging, 0);
        }
    }
}
