using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Navigation;

using EliCloudDesktop.Core;
using EliCloudDesktop.Pages;
using EliCloudDesktop.Services;
using EliCloudDesktop.Shell;
using EliCloudDesktop.Ui;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

// EliCloud 桌面客户端入口。
// 尺寸取设计稿画布（1252×800），窗口可自由缩放，布局全部按 flex 自适应。
ReactorApp.Run<App>("EliCloud Desktop", DesignTokens.WindowWidth, DesignTokens.WindowHeight);

/// <summary>
/// 应用根组件：托管窗口标题栏，持有全局状态与 SDK 网关，并按登录状态在登录页 / 主框架之间切换。
/// </summary>
/// <remarks>
/// 这里刻意**不**调 <c>UseNavigation</c>：导航句柄属于「已登录的那棵树」，
/// 由 <c>MainShell</c> 创建并交给它自己的 <c>NavigationHost</c>。
/// 退出登录时 MainShell 整棵卸载，导航历史随之消失 —— 正好是登出该有的语义。
/// </remarks>
internal sealed class App : Component
{
    /// <summary>
    /// 标题栏图标必须给**文件系统路径**，不能给 ms-appx URI：
    /// WinUI 的 TitleBar 走的是 HICON/路径那套原始图元，ms-appx 会解析失败并显示成破图
    /// （Reactor 的 REACTOR_ICON_001 也提示了这条差异）。
    /// </summary>
    private static readonly string AppIconPath = Path.Combine(
        AppContext.BaseDirectory, "Assets", "Images", "elicloud-app-icon-cloud.png");

    /// <summary>启动时读一次本机记住的东西（用户名 + 主题）。</summary>
    private static readonly AppState InitialState = CreateInitialState();

    private static AppState CreateInitialState()
    {
        var settings = AppSettings.Load();
        return AppState.Empty with
        {
            Theme = settings.Theme,
            LastUsername = settings.LastUsername,
        };
    }

    public override Element Render()
    {
        // dispatch 会被后台探测任务的续体调用，所以打开 threadSafe。
        var (state, dispatch) = UseReducer<AppState, AppAction>(AppReducer.Reduce, InitialState, threadSafe: true);

        // 平台入口是构建时常量，整个进程只有一个网关实例。
        var gateway = UseMemo(
            () => new EliCloudGateway(PlatformConfig.BaseAddress),
            Array.Empty<object>());

        var isAuthenticated = state.Session.IsAuthenticated;

        // 登录后探测各服务状态：这是「可用 / 不可用」的唯一来源。
        UseEffect(
            () =>
            {
                if (!isAuthenticated) return () => { };

                var cts = new CancellationTokenSource();
                _ = ProbeAsync(cts.Token);
                return () =>
                {
                    cts.Cancel();
                    cts.Dispose();
                };

                async Task ProbeAsync(CancellationToken ct)
                {
                    try
                    {
                        var statuses = await gateway.ProbeAllAsync(ct).ConfigureAwait(true);
                        if (!ct.IsCancellationRequested)
                        {
                            dispatch(new AppAction.ServicesRefreshed(statuses));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 退出登录导致的取消，属于正常路径。
                    }
                    catch (Exception)
                    {
                        // 单个服务的失败已经在 ProbeAsync 内部转成不可用结论；
                        // 走到这里说明是意料之外的错误，保留上一次结论即可。
                    }
                }
            },
            isAuthenticated);

        // 登录后拉一次个人资料；失败不影响登录与其它页面（资料页会显示重试入口）。
        var accessToken = state.Session.AccessToken;
        UseEffect(
            () =>
            {
                if (!isAuthenticated || accessToken is not { Length: > 0 }) return () => { };

                var cts = new CancellationTokenSource();
                _ = LoadProfileAsync(cts.Token);
                return () =>
                {
                    cts.Cancel();
                    cts.Dispose();
                };

                async Task LoadProfileAsync(CancellationToken ct)
                {
                    try
                    {
                        var profile = await gateway.GetMyProfileAsync(state.Session, ct).ConfigureAwait(true);
                        if (!ct.IsCancellationRequested)
                        {
                            dispatch(new AppAction.ProfileRefreshed(profile));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 退出登录 / 换账号导致的取消，属于正常路径。
                    }
                    catch (Exception)
                    {
                        // 资料接口失败不该把用户踢回登录页；资料页保留账号信息与重试按钮。
                    }
                }
            },
            accessToken);

        // 用户名与主题落盘。
        UseEffect(
            () => new AppSettings(state.LastUsername, state.Theme).Save(),
            state.LastUsername,
            state.Theme);

        var content = isAuthenticated
            ? Component<MainShell>()
            : Component<LoginPage>();

        // 标题栏是**窗口级**的外壳，不归某一页：登录页、注册/验证页、登录后的主框架共用同一条。
        // 用官方 TitleBar（Tall 就是稿子的 48px），窗口据此推断 ExtendsContentIntoTitleBar，
        // 未登录时也就不会再退回系统自带的那条标题栏。
        // 副标题只随登录态变化（稿子：Internal 仅登录后出现）。
        var titleBar = TitleBar("EliCloud Desktop")
            .Tall()
            .Icon(AppIconPath)
            .Flex(shrink: 0);

        if (isAuthenticated)
        {
            titleBar = titleBar.Subtitle("Internal");
        }

        return FlexColumn(
                titleBar,
                // 内容要撑满标题栏以下的整块区域。这层 Border 是故意的：content 是**组件元素**，
                // grow 得落在有原生视图的元素上才有布局参与（组件元素自己不占位）。
                Border(content).Flex(grow: 1, basis: 0))
            .Provide(EliCloudContexts.State, state)
            .Provide(EliCloudContexts.Dispatch, dispatch)
            .Provide(EliCloudContexts.Gateway, gateway)
            .RequestedTheme(state.Theme switch
            {
                AppTheme.Dark => ElementTheme.Dark,
                AppTheme.Light => ElementTheme.Light,
                _ => ElementTheme.Default,
            })
            .Backdrop(BackdropKind.Mica);
    }
}
