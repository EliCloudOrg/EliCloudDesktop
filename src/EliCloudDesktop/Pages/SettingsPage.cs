using EliCloudDesktop.Core;
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

    private static readonly string[] ThemeNames = ["浅色", "深色", "跟随系统"];

    private static int ThemeIndex(AppTheme theme) => Math.Max(0, Array.IndexOf(Themes, theme));

    public override Element Render()
    {
        var state = UseContext(EliCloudContexts.State);
        var dispatch = UseContext(EliCloudContexts.Dispatch);

        var cards = new List<Element>
        {
            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE77B"),
                "当前账号",
                state.Session.IsAuthenticated ? state.Session.DisplayEmail : "还没有登录",
                trailing: state.Session.IsAuthenticated
                    ? Button("退出登录", () => dispatch(new AppAction.SignedOut()))
                    : null,
                actionToolTip: "退出登录"),

            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE790"),
                "外观",
                "浅色、深色，或者跟着 Windows 走",
                trailing: ComboBox(ThemeNames, ThemeIndex(state.Theme), index =>
                {
                    if (index >= 0 && index < Themes.Length)
                    {
                        dispatch(new AppAction.ThemeChanged(Themes[index]));
                    }
                }).MinWidth(150)),

            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE946"),
                "关于",
                "EliCloud 桌面版 0.1"),
        };

        return FlexColumn(
            Widgets.PageHeader("设置", "账号与应用信息"),

            ScrollView(VStack(4, [.. cards])
                .Margin(DesignTokens.ContentPadding, 4, DesignTokens.ContentPadding, 40))
                .Flex(grow: 1, basis: 0));
    }
}
