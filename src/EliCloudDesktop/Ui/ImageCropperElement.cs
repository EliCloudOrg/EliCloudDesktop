using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Reactor.Wrappers;

namespace EliCloudDesktop.Ui;

/// <summary>
/// 把 Windows Community Toolkit 的 <see cref="ImageCropper"/> 包成 Reactor 的声明式元素。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="SettingsCardElement"/> 同一套路：控件来自 NuGet 包，由源生成器产出
/// init-props + 工厂方法，因此工程里仍然没有任何 .xaml 文件。
/// </para>
/// <para>
/// 只暴露真正要用的成员：<c>cropShape</c> 决定圆形/方形选区，<c>aspectRatio</c> 给之后
/// 需要固定比例的裁切留口子，<c>source</c> 是 <c>WriteableBitmap</c> 类型的图片源
/// （头像走 <c>LoadImageFromFile</c>，所以平时不传）。裁切结果经控件的 <c>SaveAsync</c> 取走，
/// 不需要再往元素上包更多成员。
/// </para>
/// </remarks>
[GenerateReactorWrapper(
    typeof(ImageCropper),
    AutoDiscover = false,
    Include = new[] { "Source", "AspectRatio", "CropShape" })]
internal sealed partial record ImageCropperElement;
