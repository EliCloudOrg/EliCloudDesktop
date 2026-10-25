using System.Text.RegularExpressions;
using EliCloud.Sdk;
using EliCloud.Sdk.Mc;
using EliCloudDesktop.Core;
using EliCloudDesktop.Ui;
using Microsoft.UI.Reactor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Navigation;

namespace EliCloudDesktop.Pages;

/// <summary>
/// MC 服务器白名单页 —— 设计稿没有画这一页，按主页的视觉语言自行设计。
/// </summary>
/// <remarks>
/// 数据来自已上线的 mc 服务：健康快照、我的条目与名额、提交、撤回。
/// 页面上只说用户关心的事（能不能用、加了哪些名字、还能加几个），
/// 不出现端点路径、RCON、版本号这类实现细节。
/// </remarks>
internal sealed partial class McServicePage : Component
{
    /// <summary>服务端对 MC 用户名的硬约束：<c>^[A-Za-z0-9_]{3,16}$</c>。</summary>
    [GeneratedRegex("^[A-Za-z0-9_]{3,16}$")]
    private static partial Regex MinecraftNamePattern { get; }

    /// <summary>备注在服务端的长度上限（1–200 字符）。</summary>
    private const int NoteMaxLength = 200;

    public override Element Render()
    {
        var state = UseContext(EliCloudContexts.State);
        var gateway = UseContext(EliCloudContexts.Gateway);

        var (reload, bumpReload) = UseReducer(0);
        var (health, setHealth) = UseState<McHealthStatus?>(null);
        var (mine, setMine) = UseState<McNameListResponse?>(null);
        var (loadError, setLoadError) = UseState<string?>(null);
        var (loading, setLoading) = UseState(true);

        var (addOpen, setAddOpen) = UseState(false);
        var (draftName, setDraftName) = UseState(string.Empty);
        var (draftNote, setDraftNote) = UseState(string.Empty);
        var (submitting, setSubmitting) = UseState(false);
        var (formError, setFormError) = UseState<string?>(null);
        var (removingId, setRemovingId) = UseState<string?>(null);

        var session = state.Session;

        UseEffect(
            () =>
            {
                var cts = new CancellationTokenSource();
                _ = LoadAsync(cts.Token);
                return () =>
                {
                    cts.Cancel();
                    cts.Dispose();
                };

                async Task LoadAsync(CancellationToken ct)
                {
                    setLoading(true);
                    setLoadError(null);

                    try
                    {
                        setHealth(await gateway.GetMcHealthAsync(ct));
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception) { setLoadError(EliCloudErrorText.CannotReachServer); }

                    try
                    {
                        setMine(await gateway.ListMyMcNamesAsync(session, false, ct));
                    }
                    catch (OperationCanceledException) { return; }
                    catch (EliCloudApiException ex) { setLoadError(EliCloudErrorText.Describe(ex)); }
                    catch (Exception) { setLoadError(EliCloudErrorText.CannotReachServer); }
                    finally { setLoading(false); }
                }
            },
            reload);

        // 「现在能不能加一个」的唯一判据：名额确定用完了、或者上一次提交还没回来。
        // 名额读不到（网络抖动）时不拦 —— 那是读失败，不该把它变成写失败。
        var quotaExhausted = mine?.Quota is { Limit: > 0, Remaining: <= 0 };
        var canOpenAdd = !submitting && !quotaExhausted;

        // 弹窗里主按钮的可用状态：用户名要过服务端的硬约束，备注不能为空。
        var canSubmit = !submitting
            && MinecraftNamePattern.IsMatch(draftName.Trim())
            && draftNote.Trim().Length is > 0 and <= NoteMaxLength;

        return FlexColumn(
            Widgets.PageHeader(
                "Minecraft 玩家服务器",
                "绑定用户名以获取准入权限",
                actions: FlexRow(
                    // 提交时弹窗已经关了，这里留一个转圈，免得页面上只剩「按钮变灰」。
                    submitting
                        ? ProgressRing().Width(16).Height(16).AutomationName("正在添加用户名")
                        : null,
                    Button("添加用户名", OpenAddDialog)
                        .AccentButton()
                        .IsEnabled(canOpenAdd),
                    Button("刷新", () => bumpReload(n => n + 1))) with
                {
                    ColumnGap = 8,
                    AlignItems = FlexAlign.Center,
                }),

            ScrollView(VStack(
                16,
                StatusSection(health, mine, loading, loadError),
                WhitelistSection(
                    mine,
                    removingId,
                    formError,
                    entryId =>
                    {
                        setRemovingId(entryId);
                        _ = RemoveAsync(entryId);
                    }))
                .Margin(DesignTokens.ContentPadding, 4, DesignTokens.ContentPadding, 40))
                .Flex(grow: 1, basis: 0),

            // 弹窗常驻在树里，由 IsOpen 控制显隐；关掉它不会卸载页面。
            AddNameDialog(
                addOpen,
                draftName, setDraftName,
                draftNote, setDraftNote,
                canSubmit,
                HandleAddDialogClosed));

        void OpenAddDialog()
        {
            if (!canOpenAdd) return;
            setFormError(null);
            setAddOpen(true);
        }

        void HandleAddDialogClosed(ContentDialogResult result)
        {
            setAddOpen(false);

            // 只有「添加」会提交；「取消」和点遮罩都是 None。
            if (result != ContentDialogResult.Primary) return;

            _ = SubmitAsync();
        }

        async Task SubmitAsync()
        {
            setSubmitting(true);
            setFormError(null);

            try
            {
                await gateway.AddMcNameAsync(session, draftName.Trim(), draftNote.Trim(), CancellationToken.None);
                setDraftName(string.Empty);
                setDraftNote(string.Empty);
                bumpReload(n => n + 1);
            }
            catch (EliCloudApiException ex)
            {
                setFormError(EliCloudErrorText.Describe(ex));
            }
            catch (Exception)
            {
                setFormError(EliCloudErrorText.CannotReachServer);
            }
            finally
            {
                setSubmitting(false);
            }
        }

        async Task RemoveAsync(string entryId)
        {
            try
            {
                await gateway.RemoveMcNameAsync(session, entryId, CancellationToken.None);
                bumpReload(n => n + 1);
            }
            catch (EliCloudApiException ex)
            {
                setLoadError(EliCloudErrorText.Describe(ex));
            }
            catch (Exception)
            {
                setLoadError(EliCloudErrorText.CannotReachServer);
            }
            finally
            {
                setRemovingId(null);
            }
        }
    }

    // ── 服务器状态 ────────────────────────────────────────────────────

    private static Element StatusSection(
        McHealthStatus? health,
        McNameListResponse? mine,
        bool loading,
        string? error)
    {
        var cards = new List<Element>
        {
            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE946"),
                "服务器",
                health is null
                    ? "正在查看…"
                    : health.IsHealthy ? "运行正常" : "暂时不可用",
                trailing: loading
                    ? ProgressRing().Width(16).Height(16)
                    : Widgets.StatusBadge(health?.IsHealthy == true
                        ? ServiceAvailability.Available
                        : ServiceAvailability.Unavailable)),

            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE839"),
                "连接速度",
                health?.Rcon is not { } rcon
                    ? "正在查看…"
                    : rcon.Reachable ? $"延迟 {rcon.LatencyMs ?? 0} 毫秒" : "暂时连不上",
                trailing: health?.Rcon is { } status
                    ? Widgets.StatusBadge(status.Reachable ? ServiceAvailability.Available : ServiceAvailability.Unavailable)
                    : null),

            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE716"),
                "服务器上的白名单",
                health?.WhitelistCount is { } count ? $"共 {count} 人" : "正在查看…"),

            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE8FD"),
                "我的名额",
                mine?.Quota is { } quota
                    ? quota.Remaining > 0
                        ? $"还能添加 {quota.Remaining} 个（上限 {quota.Limit} 个）"
                        : $"名额用完了（上限 {quota.Limit} 个）"
                    : "正在查看…"),
        };

        var children = new List<Element>
        {
            Widgets.SectionHeader("服务器状态", "现在的运行情况"),
        };

        if (error is { Length: > 0 })
        {
            children.Add(InfoBar("读取失败", error).Severity(InfoBarSeverity.Warning));
        }

        children.Add(VStack(4, [.. cards]));

        return VStack(12, [.. children]);
    }

    // ── 我的白名单 ────────────────────────────────────────────────────

    private static Element WhitelistSection(
        McNameListResponse? mine,
        string? removingId,
        string? addError,
        Action<string> onRemove)
    {
        var rows = new List<Element>();
        var active = mine?.Names.Where(name => name.Status == McNameStatus.Active).ToList() ?? [];

        if (active.Count == 0)
        {
            rows.Add(DesignTokens.Body(mine is null
                    ? "正在读取…"
                    : "还没有添加用户名。点右上角的「添加用户名」就能加一个。")
                .Foreground(Theme.SecondaryText)
                .TextWrapping());
        }
        else
        {
            foreach (var entry in active)
            {
                var parts = new List<string>();
                if (entry.CreatedAt is { Length: > 0 } created) parts.Add($"添加于 {created}");
                if (entry.Note is { Length: > 0 } note) parts.Add(note);

                rows.Add(Widgets.SettingCard(
                    Widgets.ServiceIcon(ServiceCatalog.Minecraft.IconUri),
                    entry.NameDisplay ?? entry.Name ?? "（没有名字）",
                    string.Join(" · ", parts),
                    trailing: removingId == entry.Id
                        ? ProgressRing().Width(16).Height(16)
                        : Button("撤回", () => onRemove(entry.Id))
                            .IsEnabled(removingId is null)
                            .AutomationName($"撤回 {entry.Name}")));
            }
        }

        var children = new List<Element>
        {
            Widgets.SectionHeader("我的白名单", "撤回后就不能进服务器了"),
        };

        // 提交失败的提示放在列表旁边：弹窗已经关了，用户需要知道刚才那一下没成，
        // 以及再打开弹窗时输入还在（draft 不清空）。
        if (addError is { Length: > 0 })
        {
            children.Add(InfoBar("添加失败", addError).Severity(InfoBarSeverity.Error));
        }

        // 添加按钮被禁用时，这里得说清楚为什么 —— 禁用的按钮收不到 tooltip。
        if (mine?.Quota is { Limit: > 0, Remaining: <= 0 } full)
        {
            children.Add(DesignTokens.Hint($"名额已经用完了（上限 {full.Limit} 个），要加新名字得先撤回一个。"));
        }

        children.Add(VStack(4, [.. rows]));

        return VStack(12, [.. children]);
    }

    // ── 添加用户名（弹窗） ────────────────────────────────────────────

    /// <summary>
    /// 「添加用户名」的模态弹窗。主按钮的可用状态直接由输入是否合法决定。
    /// </summary>
    /// <remarks>
    /// 弹窗是声明式的：<c>IsOpen</c> 由页面状态驱动，关闭结果经 <c>OnClosed</c> 回到页面，
    /// 不调用命令式 <c>ShowAsync()</c>（Reactor 的 <c>REACTOR_DIALOG_001</c> 也是这么要求的）。
    /// </remarks>
    private static Element AddNameDialog(
        bool isOpen,
        string draftName,
        Action<string> setDraftName,
        string draftNote,
        Action<string> setDraftNote,
        bool canSubmit,
        Action<ContentDialogResult> onClosed)
    {
        var fields = new List<Element>
        {
            DesignTokens.Field(
                TextBox(draftName, setDraftName, placeholderText: "游戏里用的名字").Header("用户名"),
                "只能用字母、数字和下划线，3 到 16 位"),

            DesignTokens.Field(
                TextBox(draftNote, setDraftNote, placeholderText: "写给管理员看的说明")
                    .Header("备注")
                    .MaxLength(NoteMaxLength),
                $"写清楚为什么需要这个名额，最多 {NoteMaxLength} 字"),
        };

        return ContentDialog(
            "添加用户名",
            VStack(12, [.. fields]).Width(420),
            primaryButtonText: "添加") with
        {
            IsOpen = isOpen,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = canSubmit,
            OnClosed = onClosed,
        };
    }
}
