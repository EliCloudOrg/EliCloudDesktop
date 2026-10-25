using EliCloudDesktop.Services;

using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;

namespace EliCloudDesktop.Core;

/// <summary>
/// 树级依赖：把全局状态、dispatch 与 SDK 网关一次性提供下去，
/// 页面组件不再层层透传参数。
/// </summary>
internal static class EliCloudContexts
{
    /// <summary>当前全局状态。</summary>
    public static readonly Context<AppState> State = new(AppState.Empty);

    /// <summary>状态变更入口（来自 <c>UseReducer</c>，跨渲染稳定）。</summary>
    public static readonly Context<Action<AppAction>> Dispatch = new(_ => { });

    /// <summary>SDK 网关。</summary>
    public static readonly Context<EliCloudGateway> Gateway = new(EliCloudGateway.Default);
}
