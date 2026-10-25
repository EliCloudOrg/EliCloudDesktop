using EliCloud.Sdk;
using EliCloud.Sdk.Sso;
using EliCloudDesktop.Core;
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
                                setError("这次注册已经失效了。可以重新发送，或者返回登录。");
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
                                    setError("网络不太好，正在重试…");
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
                        Props.OnNeedsSignIn("邮箱已确认，请用刚才的密码登录。");
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
            ? $"重新发送验证邮件（{remaining} 秒）"
            : "重新发送验证邮件";

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
            DesignTokens.Display("EliCloud").Foreground(Theme.PrimaryText),
            DesignTokens.PageTitle("验证邮箱").Foreground(Theme.PrimaryText).Margin(0, 12, 0, 0),
            DesignTokens.Body($"我们已向 {Props.Email} 发送了一封验证邮件。").Foreground(Theme.PrimaryText).TextWrapping(),
            DesignTokens.Hint("打开邮件并点击「完成邮箱确认」，然后回到这里；窗口会自动继续。"),
        };

        if (terminal is null && !busy)
        {
            children.Add(FlexRow(
                ProgressRing().Width(20).Height(20),
                DesignTokens.Body("正在等待邮箱确认…").Foreground(Theme.SecondaryText)) with
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
                DesignTokens.Body("正在处理…").Foreground(Theme.SecondaryText)) with
            {
                ColumnGap = 8,
                AlignItems = FlexAlign.Center,
                JustifyContent = FlexJustify.Center,
            });
        }

        if (error is { Length: > 0 })
        {
            children.Add(InfoBar("邮箱验证", error)
                .Severity(InfoBarSeverity.Error)
                .Width(FormWidth));
        }
        else if (terminal is not null)
        {
            children.Add(InfoBar("这次注册没有完成", "可以重新发送验证邮件，或者返回登录。")
                .Severity(InfoBarSeverity.Warning)
                .Width(FormWidth));
        }

        children.Add(Button(resendLabel, Resend)
            .AccentButton()
            .Width(FormWidth)
            .IsEnabled(canResend)
            .AutomationName("重新发送验证邮件"));

        children.Add(Button("换一个邮箱", () => Cancel(Props.OnCancel))
            .Width(FormWidth)
            .IsEnabled(!busy)
            .AutomationName("换一个邮箱"));

        children.Add(Button("返回登录", () => Props.OnBackToSignIn())
            .Width(FormWidth)
            .IsEnabled(!busy)
            .AutomationName("返回登录"));

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
            ? "验证链接已过期。可以重新发送，或者返回登录。"
            : status.Reason switch
            {
                "username_taken" => "这个用户名刚刚被占用了。换一个用户名再试。",
                "email_taken" => "这个邮箱刚刚被注册了。可用别的邮箱，或返回登录。",
                _ => "这次注册没有完成。可以重新发送，或者返回登录。",
            };
}
