using EliCloudDesktop.Core;
using EliCloudDesktop.Generated;
using EliCloudDesktop.Ui;
using Microsoft.UI.Reactor;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Navigation;

namespace EliCloudDesktop.Pages;

/// <summary>
/// 设置页 —— 由 Nav 内置的「设置」项进入（设计稿 Nav 页脚第二项）。
/// </summary>
internal sealed class SettingsPage : Component
{
    private static readonly AppTheme[] Themes = [AppTheme.Light, AppTheme.Dark, AppTheme.System];

    private static int ThemeIndex(AppTheme theme) => Math.Max(0, Array.IndexOf(Themes, theme));

    public override Element Render()
    {
        var state = UseContext(EliCloudContexts.State);
        var dispatch = UseContext(EliCloudContexts.Dispatch);

        // ComboBox 的 ItemsSource 按引用 diff：每次 Render 新建 string[] 会把选择重置，
        // 外观选择器看起来就像「选不中」。文案是静态的，整页生命周期只建一次。
        var themeNames = UseMemo(
            () => new[]
            {
                Copy.Get(CopyKeys.SettingsAppearanceLight),
                Copy.Get(CopyKeys.SettingsAppearanceDark),
                Copy.Get(CopyKeys.SettingsAppearanceSystem),
            },
            Array.Empty<object>());

        var signOutLabel = Copy.Get(CopyKeys.SettingsAccountSignOut);

        var cards = new List<Element>
        {
            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE77B"),
                Copy.Get(CopyKeys.SettingsAccountTitle),
                state.Session.IsAuthenticated
                    ? state.Session.DisplayEmail
                    : Copy.Get(CopyKeys.SettingsAccountSignedOut),
                trailing: state.Session.IsAuthenticated
                    ? Button(signOutLabel, () => dispatch(new AppAction.SignedOut()))
                        .AutomationName(signOutLabel)
                    : null,
                actionToolTip: signOutLabel),

            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE790"),
                Copy.Get(CopyKeys.SettingsAppearanceTitle),
                Copy.Get(CopyKeys.SettingsAppearanceDescription),
                trailing: ComboBox(themeNames, ThemeIndex(state.Theme), index =>
                {
                    if (index >= 0 && index < Themes.Length)
                    {
                        dispatch(new AppAction.ThemeChanged(Themes[index]));
                    }
                }).MinWidth(150).AutomationName(Copy.Get(CopyKeys.SettingsAppearanceTitle))),

            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE946"),
                Copy.Get(CopyKeys.SettingsAboutTitle),
                Copy.Format(CopyKeys.SettingsAboutVersion, ("version", PlatformConfig.AppVersion))),
        };

        return FlexColumn(
            Widgets.PageHeader(
                Copy.Get(CopyKeys.SettingsTitle),
                Copy.Get(CopyKeys.SettingsDescription)),

            ScrollView(VStack(4, [.. cards])
                .Margin(DesignTokens.ContentPadding, 4, DesignTokens.ContentPadding, 40))
                .Flex(grow: 1, basis: 0));
    }
}
