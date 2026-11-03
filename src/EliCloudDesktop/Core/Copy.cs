using System.Globalization;

namespace EliCloudDesktop.Core;

/// <summary>
/// 用户可见文案的唯一读取入口：key → 生成的 <c>Strings/zh-CN/Resources.resw</c>。
/// </summary>
/// <remarks>
/// <para>
/// 文案源在 <c>contracts/copy/zh-CN.json</c>，由 <c>tools/gen-copy</c> 生成
/// <c>.resw</c>（文案本体）与 <c>Generated/CopyKeys.g.cs</c>（key 常量）——
/// 客户端里**没有第二份文案**，页面只写 key。
/// </para>
/// <para>
/// 具体资源读取器由外壳在启动时注入（<see cref="Resolver"/>，实现见
/// <c>Services/MrtCopyResolver.cs</c>）：Core 因此不依赖 WinUI/MRT，
/// 单元测试进程里 <see cref="Resolver"/> 为 null，读取回落到 key —— 测试断言的是 key 与映射，
/// 界面在缺资源时也至少能看到 key 而不是空白。
/// </para>
/// <para>
/// 占位符统一是 <c>{name}</c>（见 copy 源的 <c>placeholders</c>），由
/// <see cref="Format"/> 做字面替换；不用 <see cref="string.Format(string,object?)"/>，
/// 因为文案里的花括号不是 .NET 格式项，转义只会让四端文案漂移。
/// </para>
/// </remarks>
internal static class Copy
{
    /// <summary>资源读取器（key → 文案）；应用启动时注入，测试里保持 null。</summary>
    public static Func<string, string?>? Resolver { get; set; }

    /// <summary>取一条文案；资源不可用或缺失时回落到 key。</summary>
    public static string Get(string key)
    {
        try
        {
            var value = Resolver?.Invoke(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch (Exception)
        {
            // resources.pri 尚未就绪 / 非打包进程：只影响文案，不影响功能。
            return key;
        }
    }

    /// <summary>取一条文案并替换 <c>{name}</c> 占位符。</summary>
    public static string Format(string key, params (string Name, object? Value)[] values)
    {
        var text = Get(key);
        foreach (var (name, value) in values)
        {
            var replacement = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            text = text.Replace("{" + name + "}", replacement, StringComparison.Ordinal);
        }

        return text;
    }
}
