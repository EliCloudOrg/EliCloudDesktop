using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace EliCloudDesktop.Services;

/// <summary>
/// 把 userinfo 代理回来的图片字节转成 <see cref="BitmapImage"/>，按 sha256 缓存。
/// </summary>
/// <remarks>
/// WinUI 的 <see cref="BitmapImage"/> 有 UI 线程亲和性，所以这个类只在 UI 线程调用
/// （页面 / Nav 的异步 effect 都回到 UI 上下文）。图片变化时 sha256 一定变化，缓存键是稳定的。
/// </remarks>
internal static class ProfileImageCache
{
    private const int MaxEntries = 64;
    private static readonly Dictionary<string, BitmapImage> Cache = new(StringComparer.Ordinal);

    public static async Task<BitmapImage> LoadAsync(byte[] bytes, string sha256, CancellationToken ct)
    {
        if (Cache.TryGetValue(sha256, out var cached))
        {
            return cached;
        }

        ct.ThrowIfCancellationRequested();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        ct.ThrowIfCancellationRequested();
        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);

        if (Cache.Count >= MaxEntries)
        {
            Cache.Clear();
        }
        Cache[sha256] = bitmap;
        return bitmap;
    }
}
