using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace EliCloudDesktop.Services;

/// <summary>
/// 头像 / 头图的后处理：把 <c>ImageCropper</c> 的裁切结果收敛成固定尺寸内的 PNG 字节。
/// </summary>
/// <remarks>
/// <para>
/// 为什么需要它：<c>ImageCropper.SaveAsync</c> 是按<b>原图像素</b>裁的，一张 4000×3000 的
/// 照片裁成正方形仍有 3000×3000，编成 PNG 往往十几 MB，直接撞上头像 5 MB 的上传上限，
/// 也白占 Nav 与资料页的解码内存。头像实际只显示 64×64，512 已经够高 DPI 用了；
/// 头图按 932×183 显示，1600 宽封顶。
/// </para>
/// <para>
/// 这里只处理<b>已经裁好</b>的 PNG（没有 EXIF 方向问题），所以缩放时用
/// <see cref="ExifOrientationMode.IgnoreExifOrientation"/> 即可。
/// 解码 / 编码都留在 UI 线程上（WinUI 图片 API 的线程亲和性），调用方在 UI 上下文里 await。
/// </para>
/// </remarks>
internal static class ProfileImageProcessing
{
    /// <summary>头像的最长边（像素）。显示尺寸 64，512 足够应付高 DPI 与后续放大展示。</summary>
    public const int AvatarMaxEdge = 512;

    /// <summary>头图的最长边（像素）。页头横幅按 932×183 显示，1600 宽足够 2x 屏。</summary>
    public const int BannerMaxEdge = 1600;

    /// <summary>把用户挑的图片解码成 <c>ImageCropper.Source</c> 需要的 <see cref="WriteableBitmap"/>。</summary>
    /// <remarks>
    /// ImageCropper 的 Source 类型就是 WriteableBitmap（不是通用的 ImageSource），
    /// 而且它保存裁切结果时读的正是这块像素，所以这里不做 EXIF 旋转/缩放：
    /// 屏幕上看到的方向、坐标，和 SaveAsync 取走的像素必须是同一份，否则裁出来的区域会错位。
    /// </remarks>
    public static async Task<WriteableBitmap> LoadCropSourceAsync(byte[] bytes)
    {
        using var stream = await ToRandomAccessStreamAsync(bytes);
        var bitmap = new WriteableBitmap(1, 1);
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    /// <summary>把裁切结果缩放到 <paramref name="maxEdge"/> 以内，并统一重编码为 PNG。</summary>
    public static async Task<byte[]> FitAsync(byte[] pngBytes, int maxEdge)
    {
        using var input = await ToRandomAccessStreamAsync(pngBytes);
        var decoder = await BitmapDecoder.CreateAsync(input);

        var scale = Math.Min(1d, (double)maxEdge / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var width = Math.Max(1u, (uint)Math.Round(decoder.PixelWidth * scale));
        var height = Math.Max(1u, (uint)Math.Round(decoder.PixelHeight * scale));

        var transform = new BitmapTransform
        {
            ScaledWidth = width,
            ScaledHeight = height,
            InterpolationMode = BitmapInterpolationMode.Fant,
        };

        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.ColorManageToSRgb);

        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            width,
            height,
            96,
            96,
            pixelData.DetachPixelData());
        await encoder.FlushAsync();

        return await ReadAllBytesAsync(output);
    }

    /// <summary>把内存流整个读成字节数组。</summary>
    public static async Task<byte[]> ReadAllBytesAsync(IRandomAccessStream stream)
    {
        stream.Seek(0);
        var size = checked((int)stream.Size);
        var bytes = new byte[size];
        using var reader = new DataReader(stream);
        var offset = 0;
        while (offset < size)
        {
            var loaded = await reader.LoadAsync((uint)(size - offset));
            if (loaded == 0) break;
            var chunk = new byte[loaded];
            reader.ReadBytes(chunk);
            System.Buffer.BlockCopy(chunk, 0, bytes, offset, (int)loaded);
            offset += (int)loaded;
        }

        return bytes;
    }

    private static async Task<InMemoryRandomAccessStream> ToRandomAccessStreamAsync(byte[] bytes)
    {
        var stream = new InMemoryRandomAccessStream();
        using var writer = new DataWriter(stream);
        writer.WriteBytes(bytes);
        await writer.StoreAsync();
        await writer.FlushAsync();
        writer.DetachStream();
        stream.Seek(0);
        return stream;
    }
}
