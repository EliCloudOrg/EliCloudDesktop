namespace EliCloudDesktop.Core;

/// <summary>
/// 平台入口地址 —— 构建时注入，运行时只读。
/// </summary>
/// <remarks>
/// <para>
/// 值来自构建属性 <c>EliCloudPlatformBaseAddress</c>（按配置给默认值，见 <c>EliCloudDesktop.csproj</c>）：
/// </para>
/// <list type="bullet">
///   <item><c>Debug-Local</c> → 本机服务端；</item>
///   <item><c>Debug-Prod</c> / <c>Debug</c> / <c>Release</c> → 远程平台；</item>
///   <item>也可以用 <c>-p:EliCloudPlatformBaseAddress=…</c> 显式覆盖（优先）。</item>
/// </list>
/// <para>
/// <b>故意不提供任何运行时入口</b>：
/// </para>
/// <list type="bullet">
///   <item>没有设置项 —— 它是我方基础设施，不是用户偏好；</item>
///   <item>不读环境变量、不读配置文件 —— 用户改不了，第三方也注入不进来；</item>
///   <item>界面上不显示 —— 用户只需要知道「EliCloud 能用」。</item>
/// </list>
/// <para>
/// 值由 csproj 的 <c>GeneratePlatformConfig</c> 目标生成成编译期常量
/// （<see cref="InjectedBaseAddress"/>），而不是运行时反射读程序集元数据：
/// Release 是 <c>PublishTrimmed=true</c>，常量对裁剪免疫，启动时也没有反射开销。
/// 程序集元数据同样带着这个值，方便直接从构建产物看出它是为哪个入口构建的。
/// </para>
/// </remarks>
internal static partial class PlatformConfig
{
    /// <summary>形如 <c>https://host[:port]</c> 的平台入口（已规范化：无尾斜杠、无路径）。</summary>
    public static string BaseAddress { get; } = Normalize(InjectedBaseAddress);

    private static string Normalize(string value)
    {
        if (!Uri.TryCreate(value?.Trim().TrimEnd('/'), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(BuildError(value, "不是一个合法的绝对地址"));
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            throw new InvalidOperationException(BuildError(value, "只接受 http / https"));
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static string BuildError(string? value, string reason) =>
        $"构建配置有误：EliCloudPlatformBaseAddress {reason}（当前值：'{value}'）。" +
        "请用 -c Debug-Prod / -c Debug-Local 选择环境，" +
        "或显式传 -p:EliCloudPlatformBaseAddress=https://your-entry 重新构建。";
}
