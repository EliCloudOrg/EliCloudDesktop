using EliCloud.Sdk;
using EliCloud.Sdk.Sso;
using EliCloudDesktop.Core;
using EliCloudDesktop.Generated;
using EliCloudDesktop.Ui;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Microsoft.UI.Reactor.Factories;

namespace EliCloudDesktop.Pages;

/// <summary>发起注册后、邮箱确认前展示的验证页所需的全部数据。</summary>
internal sealed record VerifyEmailProps(
    string RegistrationId,
    string Username,
    string Email,
    string Password,
    DateTimeOffset? ExpiresAt,
    int ResendAfter,
    Action<SessionState> OnVerified,
    Action<string> OnNeedsSignIn,
    Action OnCancel,
    Action OnBackToSignIn);

/// <summary>
/// 等待邮箱确认：轮询状态、重发、取消，确认后自动用内存中的密码登录。
/// </summary>
/// <remarks>
/// 密码只在本组件的 props 里短暂存在；不写设置文件、不进日志。
/// 所有状态更新都在 UI 线程（await 不 ConfigureAwait(false)）。
/// </remarks>
internal sealed class VerifyEmailPage : Component<VerifyEmailProps>
{
    private const double FormWidth = 431;

    public override Element Render()
    {
        var gateway = UseContext(EliCloudContexts.Gateway);

        var (busy, setBusy) = UseState(false);
        var (error, setError) = UseState<string?>(null);
        var (terminal, setTerminal) = UseState<string?>(null);
        var (now, setNow) = UseState(DateTimeOffset.UtcNow);
        var (resendAt, setResendAt) = UseState(DateTimeOffset.UtcNow.AddSeconds(Props.ResendAfter));
        // 从 expired/failed 重发成功后，轮询 effect 已经返回；递增它来重启轮询。
        var (pollGeneration, setPollGeneration) = UseState(0);

        // 1 秒刷新一次冷却显示；卸载时取消。
        UseEffect(
            () =>
            {
                var cts = new CancellationTokenSource();
                _ = TickAsync(cts.Token);
                return () =>
                {
                    cts.Cancel();
                    cts.Dispose();
                };

                async Task TickAsync(CancellationToken ct)
                {
                    try
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            setNow(DateTimeOffset.UtcNow);
                            await Task.Delay(TimeSpan.FromSeconds(1), ct);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 卸载/模式切换，正常路径。
                    }
                }
            },
            Props.RegistrationId);

        // 轮询：进入页面立即查一次，之后 2 秒；网络失败退避到最多 10 秒。
        UseEffect(
            () =>
            {
                var cts = new CancellationTokenSource();
                _ = PollAsync(cts.Token);
                return () =>
                {
                    cts.Cancel();
                    cts.Dispose();
                };

                async Task PollAsync(CancellationToken ct)
                {
                    var failures = 0;
                    try
                    {
                        while (!ct.IsCancellationRequested)
                        {
                            try
                            {
                                var status = await gateway
                                    .GetRegistrationStatusAsync(Props.RegistrationId, ct)
                                    .ConfigureAwait(true);

                                failures = 0;

                                if (status.IsVerified)
                                {
                                    await FinishAsync(ct);
                                    return;
                                }

                                if (status.Status is RegistrationStatuses.Expired or RegistrationStatuses.Failed)
                                {
                                    setTerminal(status.Status);
                                    setError(DescribeTerminal(status));
                                    return;
                                }

                                setError(null);
                                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }
                            catch (EliCloudApiException ex) when (
                                ex.Error is EliCloudErrorCodes.RegistrationNotFound
                                    or EliCloudErrorCodes.RegistrationExpired)
                            {
                                setTerminal(RegistrationStatuses.Expired);
                                setError(Copy.Get(CopyKeys.VerifyInvalidated));
                                return;
                            }
                            catch (EliCloudApiException ex) when (ex.IsRateLimited)
                            {
                                var delay = ex.RetryAfter ?? TimeSpan.FromSeconds(10);
                                await Task.Delay(delay, ct);
                            }
                            catch (Exception)
                            {
                                failures++;
                                if (failures >= 3)
                                {
                                    setError(Copy.Get(CopyKeys.VerifyNetworkRetry));
                                }
                                var seconds = Math.Min(10, 2 + failures * 2);
                                await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常退出。
                    }
                }

                async Task FinishAsync(CancellationToken ct)
                {
                    setBusy(true);
                    setError(null);
                    try
                    {
                        var session = await gateway
                            .LoginAsync(Props.Username, Props.Password, ct)
                            .ConfigureAwait(true);
                        Props.OnVerified(session);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (EliCloudApiException ex)
                    {
                        Props.OnNeedsSignIn(EliCloudErrorText.Describe(ex));
                    }
                    catch (Exception)
                    {
                        Props.OnNeedsSignIn(Copy.Get(CopyKeys.VerifyConfirmedSignIn));
                    }
                    finally
                    {
                        setBusy(false);
                    }
                }
            },
            Props.RegistrationId,
            Props.ResendAfter,
            pollGeneration);

        var remaining = Math.Max(0, (int)Math.Ceiling((resendAt - now).TotalSeconds));
        // expired/failed 也允许重发：服务端会把该 pending 重新激活。
        var canResend = !busy && remaining == 0;
        var resendLabel = remaining > 0
            ? Copy.Format(CopyKeys.VerifyResendCountdown, ("seconds", remaining))
            : Copy.Get(CopyKeys.VerifyResend);

        void Resend()
        {
            if (!canResend) return;
            setBusy(true);
            setError(null);
            _ = ResendAsync();

            async Task ResendAsync()
            {
                try
                {
                    var pending = await gateway
                        .ResendRegistrationEmailAsync(Props.RegistrationId, CancellationToken.None)
                        .ConfigureAwait(true);
                    setResendAt(DateTimeOffset.UtcNow.AddSeconds(pending.ResendAfter));
                    setTerminal(null);
                    setPollGeneration(pollGeneration + 1);
                }
                catch (EliCloudApiException ex)
                {
                    setError(EliCloudErrorText.Describe(ex));
                    if (ex.RetryAfter is { } retry)
                    {
                        setResendAt(DateTimeOffset.UtcNow.Add(retry));
                    }
                }
                catch (Exception)
                {
                    setError(EliCloudErrorText.CannotReachServer);
                }
                finally
                {
                    setBusy(false);
                }
            }
        }

        void Cancel(Action after)
        {
            if (busy) return;
            setBusy(true);
            _ = CancelAsync();

            async Task CancelAsync()
            {
                try
                {
                    await gateway
                        .CancelRegistrationAsync(Props.RegistrationId, CancellationToken.None)
                        .ConfigureAwait(true);
                }
                catch (Exception)
                {
                    // 取消是 best-effort；服务端过期后也会自然清理。
                }
                finally
                {
                    setBusy(false);
                    after();
                }
            }
        }

        var children = new List<Element>
        {
            DesignTokens.Display(Copy.Get(CopyKeys.AppProductName)).Foreground(Theme.PrimaryText),
            DesignTokens.PageTitle(Copy.Get(CopyKeys.VerifyTitle))
                .Foreground(Theme.PrimaryText).Margin(0, 12, 0, 0),
            DesignTokens.Body(Copy.Format(CopyKeys.VerifySent, ("email", Props.Email)))
                .Foreground(Theme.PrimaryText).TextWrapping(),
            DesignTokens.Hint(Copy.Get(CopyKeys.VerifyInstructions)),
        };

        if (terminal is null && !busy)
        {
            children.Add(FlexRow(
                ProgressRing().Width(20).Height(20),
                DesignTokens.Body(Copy.Get(CopyKeys.VerifyWaiting)).Foreground(Theme.SecondaryText)) with
            {
                ColumnGap = 8,
                AlignItems = FlexAlign.Center,
                JustifyContent = FlexJustify.Center,
            });
        }

        if (busy)
        {
            children.Add(FlexRow(
                ProgressRing().Width(20).Height(20),
                DesignTokens.Body(Copy.Get(CopyKeys.CommonProcessing)).Foreground(Theme.SecondaryText)) with
            {
                ColumnGap = 8,
                AlignItems = FlexAlign.Center,
                JustifyContent = FlexJustify.Center,
            });
        }

        if (error is { Length: > 0 })
        {
            children.Add(InfoBar(Copy.Get(CopyKeys.VerifyFailedTitle), error)
                .Severity(InfoBarSeverity.Error)
                .Width(FormWidth));
        }
        else if (terminal is not null)
        {
            children.Add(InfoBar(
                    Copy.Get(CopyKeys.VerifyTerminalTitle),
                    Copy.Get(CopyKeys.VerifyTerminalDetail))
                .Severity(InfoBarSeverity.Warning)
                .Width(FormWidth));
        }

        children.Add(Button(resendLabel, Resend)
            .AccentButton()
            .Width(FormWidth)
            .IsEnabled(canResend)
            .AutomationName(Copy.Get(CopyKeys.VerifyResend)));

        var changeEmailLabel = Copy.Get(CopyKeys.VerifyChangeEmail);
        children.Add(Button(changeEmailLabel, () => Cancel(Props.OnCancel))
            .Width(FormWidth)
            .IsEnabled(!busy)
            .AutomationName(changeEmailLabel));

        var backToLoginLabel = Copy.Get(CopyKeys.VerifyBackToLogin);
        children.Add(Button(backToLoginLabel, () => Props.OnBackToSignIn())
            .Width(FormWidth)
            .IsEnabled(!busy)
            .AutomationName(backToLoginLabel));

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Star()],
            children:
            [
                Border((FlexColumn([.. children]) with
                {
                    RowGap = 12,
                    AlignItems = FlexAlign.Center,
                    JustifyContent = FlexJustify.Center,
                }).Flex(grow: 1, basis: 0))
                .Grid(row: 0, column: 0),
            ]);
    }

    private static string DescribeTerminal(RegistrationStatus status) =>
        status.Status == RegistrationStatuses.Expired
            ? Copy.Get(CopyKeys.VerifyExpired)
            : status.Reason switch
            {
                "username_taken" => Copy.Get(CopyKeys.VerifyUsernameTaken),
                "email_taken" => Copy.Get(CopyKeys.VerifyEmailTaken),
                _ => Copy.Get(CopyKeys.VerifyIncomplete),
            };
}
