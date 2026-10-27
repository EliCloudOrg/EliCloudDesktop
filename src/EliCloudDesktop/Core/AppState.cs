namespace EliCloudDesktop.Core;

/// <summary>
/// 全局应用状态（单一事实来源）。所有会驱动 UI 变化的东西都放在这里，
/// 页面组件只从 Context 里读它、只通过 dispatch 改它。
/// </summary>
internal sealed record AppState(
    SessionState Session,
    ServiceStatusSnapshot Services,
    AppTheme Theme,
    string? LastUsername,
    UserProfile? Profile)
{
    /// <summary>未登录、未探测、主题跟随系统、没有历史用户名与资料的初始状态。</summary>
    public static readonly AppState Empty =
        new(SessionState.SignedOut, ServiceStatusSnapshot.Empty, AppTheme.System, null, null);
}

/// <summary>所有状态变更意图。</summary>
internal abstract record AppAction
{
    public sealed record SignedIn(SessionState Session) : AppAction;

    public sealed record SignedOut : AppAction;

    public sealed record ServicesRefreshed(IReadOnlyList<ServiceStatus> Statuses) : AppAction;

    public sealed record ThemeChanged(AppTheme Theme) : AppAction;

    /// <summary>登录后成功拉到 /userinfo/v1/profile（或保存后拿到最新快照）。</summary>
    public sealed record ProfileRefreshed(UserProfile Profile) : AppAction;
}

/// <summary>纯函数 reducer：Reactor 的 UseReducer 只需要这一个入口。</summary>
internal static class AppReducer
{
    public static AppState Reduce(AppState state, AppAction action) => action switch
    {
        // 记下用户名：退出登录后回到登录页时可以直接带出来。
        // 新会话先清空资料，避免上一个账号的头像/昵称短暂串到新账号。
        AppAction.SignedIn a => state with
        {
            Session = a.Session,
            LastUsername = a.Session.Username ?? state.LastUsername,
            Profile = null,
        },

        // 退出登录必须把服务状态与资料一起清掉。
        AppAction.SignedOut => state with
        {
            Session = SessionState.SignedOut,
            Services = ServiceStatusSnapshot.Empty,
            Profile = null,
        },

        AppAction.ServicesRefreshed a => state with
        {
            Services = new ServiceStatusSnapshot(a.Statuses),
        },

        AppAction.ThemeChanged a => state with
        {
            Theme = a.Theme,
        },

        AppAction.ProfileRefreshed a => state with
        {
            Profile = a.Profile,
        },

        _ => state,
    };
}
