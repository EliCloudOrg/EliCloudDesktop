using Microsoft.Windows.ApplicationModel.Resources;

namespace EliCloudDesktop.Services;

/// <summary>
/// 用 MRT 实现文案读取，由 <c>App</c> 在启动时注入给 <see cref="EliCloudDesktop.Core.Copy"/>。
/// </summary>
/// <remarks>
/// <para>
/// 放在应用（而不是 Core）里，是为了让 Core／单测不依赖 WinUI/MRT：
/// 单测进程没有 resources.pri 与 package identity，注入点保持为 null 即可。
/// </para>
/// <para>
/// 为什么用 <see cref="ResourceManager"/> + <c>MainResourceMap.GetSubtree("Resources")</c>
/// 而不是 <see cref="ResourceLoader"/>：在本项目的单项目 MSIX / 打包进程里，
/// <c>new ResourceLoader()</c> 抛 <c>COMException</c>、<c>new ResourceLoader("Resources")</c>
/// 抛 <c>FileNotFoundException</c>；而 ResourceMap 路径实测可用（见 Stage 3 验证记录）。
/// </para>
/// </remarks>
internal static class MrtCopyResolver
{
    private static readonly ResourceManager Manager = new();
    private static readonly ResourceMap Resources = Manager.MainResourceMap.GetSubtree("Resources");

    /// <summary>
    /// key → 文案。copy 源用点号分组（<c>home.hero.title</c>），而 resources.pri 里的
    /// 资源名是斜杠路径（<c>home/hero/title</c>，见 <c>ms-resource://…/Resources/home/hero/title</c>）。
    /// 取不到时返回 null，由 <see cref="EliCloudDesktop.Core.Copy"/> 回落到 key。
    /// </summary>
    public static string? Resolve(string key)
    {
        try
        {
            return Resources.GetValue(key.Replace('.', '/')).ValueAsString;
        }
        catch (Exception)
        {
            // 资源未就绪 / 语言没有匹配项：让 Copy 回落到 key，不影响功能。
            return null;
        }
    }
}
