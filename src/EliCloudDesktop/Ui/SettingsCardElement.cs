using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Reactor.Wrappers;

namespace EliCloudDesktop.Ui;

/// <summary>
/// 把 Windows Community Toolkit 的 <see cref="SettingsCard"/> 包成 Reactor 的声明式元素。
/// </summary>
/// <remarks>
/// <para>
/// 这就是需求 #2 里那条「React 侧写不划算就借 XAML 控件」的路子，也是本工程**唯一**一处
/// 借助外部 XAML 控件的地方：卡片的外观、悬停/按下/焦点视觉状态、键盘可达性、
/// 无障碍对等（AutomationPeer）全部由 Toolkit 的模板负责，我们只声明内容。
/// </para>
/// <para>
/// 整个工程仍然**没有任何 .xaml 文件**：控件来自 NuGet 包，由源生成器产出
/// init-props + <c>ControlDescriptor</c> + 工厂方法，调用点写起来和 Reactor 自带元素一样。
/// 生成器还会在静态构造函数里注册控件程序集，让 Toolkit 的 <c>Themes/Generic.xaml</c>
/// 在首次实现前就位。
/// </para>
/// <para>
/// 暴露出来的成员：<c>header</c> / <c>description</c> /
/// <c>headerIcon</c> / <c>actionIcon</c> / <c>actionIconToolTip</c> /
/// <c>isActionIconVisible</c> / <c>isClickEnabled</c> /
/// <c>content</c>（右对齐内容槽）/ <c>onClick</c>。
/// </para>
/// <para>
/// <c>HeaderIcon</c> / <c>ActionIcon</c> 是 <c>UIElement</c> 类型，生成器不会把它们当普通值属性，
/// 所以用 <see cref="WrapElementSlotAttribute"/> 声明成「次级元素槽」——
/// 于是图标那一格可以直接塞 Reactor 元素（<c>Image</c> / <c>FontIcon</c> …），
/// 和卡片其余部分一样参与 diff。
/// </para>
/// </remarks>
[GenerateReactorWrapper(typeof(SettingsCard), Exclude = new[] { "Command", "CommandParameter" })]
[WrapElementSlot("HeaderIcon")]
[WrapElementSlot("ActionIcon")]
internal sealed partial record SettingsCardElement;
