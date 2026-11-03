using EliCloud.Sdk;
using EliCloud.Sdk.Mc;
using EliCloudDesktop.Core;
using EliCloudDesktop.Generated;
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
internal sealed class McServicePage : Component
{
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

        // 弹窗里主按钮的可用状态：用户名要过服务端的硬约束，备注必须合法。
        // 判据与 contracts/validation.md §3 共用 Core/Contract.cs 里的同一份实现。
        var canSubmit = !submitting
            && Validation.IsValidMinecraftName(draftName)
            && Validation.IsValidMcNote(draftNote);

        return FlexColumn(
            Widgets.PageHeader(
                Copy.Get(CopyKeys.McTitle),
                Copy.Get(CopyKeys.McDescription),
                actions: HStack(
                    8,
                    // 提交时弹窗已经关了，这里留一个转圈，免得页面上只剩「按钮变灰」。
                    submitting
                        ? ProgressRing().Width(16).Height(16)
                            .AutomationName(Copy.Get(CopyKeys.McAdding))
                        : null,
                    Button(Copy.Get(CopyKeys.McAddOpen), OpenAddDialog)
                        .AccentButton()
                        .IsEnabled(canOpenAdd)
                        .AutomationName(Copy.Get(CopyKeys.McAddOpen)),
                    Button(Copy.Get(CopyKeys.CommonRefresh), () => bumpReload(n => n + 1))
                        .AutomationName(Copy.Get(CopyKeys.CommonRefresh))),
                actionsAtRight: true),

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
                state.Theme,
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
                Copy.Get(CopyKeys.McServerTitle),
                health is null
                    ? Copy.Get(CopyKeys.CommonLoading)
                    : Copy.Get(health.IsHealthy ? CopyKeys.StatusRunning : CopyKeys.StatusDegraded),
                trailing: loading
                    ? ProgressRing().Width(16).Height(16)
                    : Widgets.StatusBadge(health?.IsHealthy == true
                        ? ServiceAvailability.Available
                        : ServiceAvailability.Unavailable)),

            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE839"),
                Copy.Get(CopyKeys.McServerLatency),
                health?.Rcon is not { } rcon
                    ? Copy.Get(CopyKeys.CommonLoading)
                    : rcon.Reachable
                        ? Copy.Format(CopyKeys.McServerLatencyValue, ("milliseconds", (long)(rcon.LatencyMs ?? 0)))
                        : Copy.Get(CopyKeys.StatusUnreachable),
                trailing: health?.Rcon is { } status
                    ? Widgets.StatusBadge(status.Reachable ? ServiceAvailability.Available : ServiceAvailability.Unavailable)
                    : null),

            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE716"),
                Copy.Get(CopyKeys.McWhitelistTitle),
                health?.WhitelistCount is { } count
                    ? Copy.Format(CopyKeys.McWhitelistCount, ("count", count))
                    : Copy.Get(CopyKeys.CommonLoading)),

            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE8FD"),
                Copy.Get(CopyKeys.McQuotaTitle),
                mine?.Quota is { } quota
                    ? quota.Remaining > 0
                        ? Copy.Format(CopyKeys.McQuotaRemaining,
                            ("remaining", quota.Remaining), ("limit", quota.Limit))
                        : Copy.Format(CopyKeys.McQuotaExhausted, ("limit", quota.Limit))
                    : Copy.Get(CopyKeys.CommonLoading)),
        };

        var children = new List<Element>
        {
            Widgets.SectionHeader(
                Copy.Get(CopyKeys.McServerStatus),
                Copy.Get(CopyKeys.McServerStatusDescription)),
        };

        if (error is { Length: > 0 })
        {
            children.Add(InfoBar(Copy.Get(CopyKeys.CommonLoadFailed), error)
                .Severity(InfoBarSeverity.Warning));
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
                    ? Copy.Get(CopyKeys.McNamesLoading)
                    : Copy.Get(CopyKeys.McNamesEmpty))
                .Foreground(Theme.SecondaryText)
                .TextWrapping());
        }
        else
        {
            foreach (var entry in active)
            {
                var parts = new List<string>();
                if (entry.CreatedAt is { Length: > 0 } created)
                {
                    parts.Add(Copy.Format(CopyKeys.McNamesAddedAt, ("created", created)));
                }

                if (entry.Note is { Length: > 0 } note) parts.Add(note);

                var displayName = entry.NameDisplay ?? entry.Name ?? Copy.Get(CopyKeys.McNamesNameless);
                var removeLabel = Copy.Get(CopyKeys.McNamesRemove);

                rows.Add(Widgets.SettingCard(
                    Widgets.ServiceIcon(ServiceCatalog.Minecraft.IconUri),
                    displayName,
                    string.Join(" · ", parts),
                    trailing: removingId == entry.Id
                        ? ProgressRing().Width(16).Height(16)
                        : Button(removeLabel, () => onRemove(entry.Id))
                            .IsEnabled(removingId is null)
                            .AutomationName(Copy.Format(CopyKeys.McNamesRemoveNamed, ("name", displayName)))));
            }
        }

        var children = new List<Element>
        {
            Widgets.SectionHeader(
                Copy.Get(CopyKeys.McNamesTitle),
                Copy.Get(CopyKeys.McNamesRemoveHint)),
        };

        // 提交失败的提示放在列表旁边：弹窗已经关了，用户需要知道刚才那一下没成，
        // 以及再打开弹窗时输入还在（draft 不清空）。
        if (addError is { Length: > 0 })
        {
            children.Add(InfoBar(Copy.Get(CopyKeys.McAddFailed), addError)
                .Severity(InfoBarSeverity.Error));
        }

        // 添加按钮被禁用时，这里得说清楚为什么 —— 禁用的按钮收不到 tooltip。
        if (mine?.Quota is { Limit: > 0, Remaining: <= 0 } full)
        {
            children.Add(DesignTokens.Hint(
                Copy.Format(CopyKeys.McQuotaExceededDetail, ("limit", full.Limit))));
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
        AppTheme theme,
        Action<ContentDialogResult> onClosed)
    {
        var fields = new List<Element>
        {
            DesignTokens.Field(
                TextBox(
                        draftName,
                        setDraftName,
                        placeholderText: Copy.Get(CopyKeys.McAddNamePlaceholder))
                    .Header(Copy.Get(CopyKeys.McAddNameLabel)),
                Copy.Get(CopyKeys.McAddNameHint)),

            DesignTokens.Field(
                TextBox(
                        draftNote,
                        setDraftNote,
                        placeholderText: Copy.Get(CopyKeys.McAddNotePlaceholder))
                    .Header(Copy.Get(CopyKeys.McAddNoteLabel))
                    .MaxLength(Contract.McNoteMaxChars),
                Copy.Format(CopyKeys.McAddNoteHint, ("maxLength", Contract.McNoteMaxChars))),
        };

        ContentDialog? native = null;

        return (ContentDialog(
            Copy.Get(CopyKeys.McAddTitle),
            VStack(12, [.. fields]).Width(420),
            primaryButtonText: Copy.Get(CopyKeys.McAddSubmit)) with
        {
            IsOpen = isOpen,
            CloseButtonText = Copy.Get(CopyKeys.CommonCancel),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = canSubmit,
            // 弹窗在 XamlRoot 浮层上，不继承页面主题；必须等它打开之后再写给原生控件
            // （挂载时写会被随后的弹窗模板/主题解析覆盖，表现出来还是系统主题）。
            OnOpened = () =>
            {
                if (native is null) return;
                var elementTheme = theme.ToElementTheme();
                if (native.RequestedTheme != elementTheme) native.RequestedTheme = elementTheme;
            },
            OnClosed = onClosed,
        })
        .Set(dialog => native = dialog);
    }
}
