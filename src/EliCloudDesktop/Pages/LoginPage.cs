using System.Globalization;
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

using Microsoft.UI.Reactor.Navigation;

namespace EliCloudDesktop.Pages;

/// <summary>
/// 登录页 —— 对应设计稿第一个 App window（居中 431×324 的登录卡片）。
/// 注册后进入 <see cref="VerifyEmailPage"/>；邮箱确认成功才创建账号。
/// </summary>
internal sealed class LoginPage : Component
{
    private const double FormWidth = 431;

    private enum AuthMode
    {
        SignIn,
        Register,
        VerifyEmail,
    }

    /// <summary>注册页与验证页之间传递的内存状态；密码不落盘。</summary>
    private sealed record PendingRegistration(
        RegistrationPending Pending,
        string Username,
        string Email,
        string Password,
        DateTimeOffset? ExpiresAt);

    public override Element Render()
    {
        var dispatch = UseContext(EliCloudContexts.Dispatch);
        var state = UseContext(EliCloudContexts.State);
        var gateway = UseContext(EliCloudContexts.Gateway);

        var (mode, setMode) = UseState(AuthMode.SignIn);

        // 登录标识：登录时可以填用户名或邮箱，注册时**只能是用户名**（平台规则）。
        // 上次登录成功后记住它，退出再回来不用重打。
        var (identifier, setIdentifier) = UseState(state.LastUsername ?? string.Empty);

        var (password, setPassword) = UseState(string.Empty);
        var (email, setEmail) = UseState(string.Empty);
        var (busy, setBusy) = UseState(false);
        var (error, setError) = UseState<string?>(null);
        var (pending, setPending) = UseState<PendingRegistration?>(null);

        if (mode == AuthMode.VerifyEmail && pending is not null)
        {
            return Component<VerifyEmailPage, VerifyEmailProps>(new VerifyEmailProps(
                RegistrationId: pending.Pending.RegistrationId,
                Username: pending.Username,
                Email: pending.Email,
                Password: pending.Password,
                ExpiresAt: pending.ExpiresAt,
                ResendAfter: pending.Pending.ResendAfter,
                OnVerified: session => dispatch(new AppAction.SignedIn(session)),
                OnNeedsSignIn: message =>
                {
                    setPending(null);
                    setMode(AuthMode.SignIn);
                    setIdentifier(pending.Username);
                    setPassword(string.Empty);
                    setError(message);
                },
                OnCancel: () =>
                {
                    setPending(null);
                    setMode(AuthMode.Register);
                    setError(null);
                },
                OnBackToSignIn: () =>
                {
                    setPending(null);
                    setMode(AuthMode.SignIn);
                    setError(null);
                }));
        }

        var isRegister = mode == AuthMode.Register;
        var canSubmit = !busy
            && !string.IsNullOrWhiteSpace(identifier)
            && !string.IsNullOrEmpty(password)
            && (!isRegister || !string.IsNullOrWhiteSpace(email));

        void Submit()
        {
            if (!canSubmit) return;
            setBusy(true);
            setError(null);
            _ = RunAsync();

            async Task RunAsync()
            {
                try
                {
                    if (isRegister)
                    {
                        var started = await gateway.StartRegistrationAsync(
                            identifier.Trim(), password, email.Trim(), CancellationToken.None);

                        setPending(new PendingRegistration(
                            started,
                            identifier.Trim(),
                            email.Trim(),
                            password,
                            ParseExpiry(started.ExpiresAt)));

                        // 密码已经进入 pending 记录，表单字段可以清空；它只在内存里。
                        setPassword(string.Empty);
                        setMode(AuthMode.VerifyEmail);
                        return;
                    }

                    var session = await gateway.LoginAsync(identifier.Trim(), password, CancellationToken.None);
                    dispatch(new AppAction.SignedIn(session));
                }
                catch (EliCloudApiException ex)
                {
                    setError(EliCloudErrorText.Describe(ex));
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

        // 登录：这一格收「用户名或邮箱」（服务端两种都能定位账号）。
        // 注册：只收用户名 —— 平台不允许拿邮箱当用户名。
        var identifierField = isRegister
            ? TextBox(identifier, setIdentifier, placeholderText: "请输入用户名").Header("用户名")
            : TextBox(identifier, setIdentifier, placeholderText: "请输入用户名或邮箱").Header("用户名或邮箱");

        var form = new List<Element>
        {
            DesignTokens.Field(
                identifierField.Width(FormWidth),
                isRegister ? "只能用用户名，不能填邮箱" : null),
        };

        if (isRegister)
        {
            form.Add(TextBox(email, setEmail, placeholderText: "用于接收验证邮件")
                .Header("邮箱")
                .Width(FormWidth));
        }

        form.Add(PasswordBox(password, setPassword, placeholderText: "请输入密码")
            .Header("密码")
            .Width(FormWidth));

        form.Add(Button(isRegister ? "注册" : "登录", Submit)
            .AccentButton()
            .Width(FormWidth)
            .IsEnabled(canSubmit)
            .AutomationName(isRegister ? "注册" : "登录"));

        form.Add(Button(isRegister ? "返回登录" : "创建账号", () =>
        {
            setError(null);
            setPassword(string.Empty);
            setMode(isRegister ? AuthMode.SignIn : AuthMode.Register);
        }).Width(FormWidth).AutomationName(isRegister ? "返回登录" : "创建账号"));

        var children = new List<Element>
        {
            DesignTokens.Display("EliCloud").Foreground(Theme.PrimaryText),
            VStack(16, [.. form])
                .Width(FormWidth)
                .Margin(0, 16, 0, 0),
        };

        if (busy)
        {
            children.Add(FlexRow(
                ProgressRing().Width(20).Height(20),
                DesignTokens.Body(isRegister ? "正在发送验证邮件…" : "正在登录…")
                    .Foreground(Theme.SecondaryText)) with
            {
                ColumnGap = 8,
                AlignItems = FlexAlign.Center,
                JustifyContent = FlexJustify.Center,
            });
        }

        if (error is { Length: > 0 })
        {
            children.Add(InfoBar(isRegister ? "注册失败" : "登录失败", error)
                .Severity(InfoBarSeverity.Error)
                .Width(FormWidth));
        }

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

    private static DateTimeOffset? ParseExpiry(string? iso) =>
        DateTimeOffset.TryParse(
            iso,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var value)
            ? value
            : null;
}
