using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using NetrunnerSolver.Core.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace NetrunnerSolver.Windows.Imaging;

public static class ImageFile
{
    public static async Task<BgraImage> LoadAsync(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        await using var file = File.OpenRead(path);
        using var stream = file.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        if (Math.Max(decoder.PixelWidth, decoder.OrientedPixelWidth) > BgraImage.MaxDimension
            || Math.Max(decoder.PixelHeight, decoder.OrientedPixelHeight) > BgraImage.MaxDimension)
        {
            // 픽셀을 풀기 전에 머리글의 크기로 거절한다. 풀고 나면 이미 그만큼 할당한 뒤다.
            throw new ArgumentException($"Image is {decoder.PixelWidth}x{decoder.PixelHeight}; at most {BgraImage.MaxDimension} pixels per side are supported.", nameof(path));
        }

        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        return ToBgraImage(bitmap);
    }

    public static async Task SavePngAsync(BgraImage image, string path)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var memory = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, memory);
        using (var bitmap = ToSoftwareBitmap(image))
        {
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        memory.Seek(0);
        await using var output = File.Create(path);
        await memory.AsStreamForRead().CopyToAsync(output);
    }

    // 받는 쪽이 자기 버퍼로 복사하는 API에 픽셀 배열을 그대로 넘긴다. 화면 한 장(4K면 33MB)을 한 번 덜 복사한다.
    // 돌려받은 배열은 읽기만 한다.
    public static byte[] SharedPixels(BgraImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return MemoryMarshal.TryGetArray(image.PixelMemory, out var segment) && segment.Offset == 0 && segment.Count == segment.Array!.Length
            ? segment.Array
            : image.Pixels.ToArray();
    }

    // CreateCopyFromBuffer가 픽셀을 비트맵으로 복사하므로 원본 배열을 감싸기만 한다.
    internal static SoftwareBitmap ToSoftwareBitmap(BgraImage image) =>
        SoftwareBitmap.CreateCopyFromBuffer(
            SharedPixels(image).AsBuffer(),
            BitmapPixelFormat.Bgra8,
            image.Width,
            image.Height,
            BitmapAlphaMode.Premultiplied);

    private static BgraImage ToBgraImage(SoftwareBitmap bitmap)
    {
        var pixels = new byte[BgraImage.ByteCount(bitmap.PixelWidth, bitmap.PixelHeight)];
        bitmap.CopyToBuffer(pixels.AsBuffer());
        return new BgraImage(bitmap.PixelWidth, bitmap.PixelHeight, pixels);
    }
}
