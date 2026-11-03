
namespace EliCloudDesktop.Core;

/// <summary>客户端的配色主题。</summary>
/// <remarks>
/// 默认 <see cref="System"/>：不指定元素级主题，颜色随 Windows 的亮/暗设置走。
/// 设置页可以固定成浅色或深色。
/// <para>
/// 这里刻意只放纯枚举、不引 WinUI：它同时被单元测试与（未来可能拆出的）Core 程序集使用；
/// 枚举到 <c>ElementTheme</c> 的映射在 <c>Ui/AppThemeExtensions.cs</c>。
/// </para>
/// </remarks>
internal enum AppTheme
{
    /// <summary>跟随系统（默认）。</summary>
    System = 0,

    /// <summary>浅色。</summary>
    Light,

    /// <summary>深色。</summary>
    Dark,
}
