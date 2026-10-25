using EliCloud.Sdk;
using EliCloudDesktop.Core;
using EliCloudDesktop.Services;
using EliCloudDesktop.Ui;
using Microsoft.UI.Reactor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using static Microsoft.UI.Reactor.Factories;

using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Navigation;

namespace EliCloudDesktop.Pages;

/// <summary>
/// 个人资料页：昵称、简介、头像、banner 头栏图。
/// </summary>
/// <remarks>
/// <para>
/// 资料编辑是「图片先、文字后」：头像 → 头图 → 昵称/简介。任何一步失败都保留弹窗，
/// 并刷新已经成功的那部分；这样用户不会以为整次保存都没发生。
/// </para>
/// <para>
/// 图片字节来自 userinfo 的代理接口（带调用者 Bearer），客户端不直连 file，
/// 也不在本地保存图片路径。
/// </para>
/// </remarks>
internal sealed class ProfilePage : Component
{
    private const string DefaultAvatarUri = "ms-appx:///Assets/Images/user-avatar-default.png";
    private const long AvatarMaxBytes = 5 * 1024 * 1024;
    private const long BannerMaxBytes = 10 * 1024 * 1024;

    public override Element Render()
    {
        var state = UseContext(EliCloudContexts.State);
        var dispatch = UseContext(EliCloudContexts.Dispatch);
        var gateway = UseContext(EliCloudContexts.Gateway);
        var window = UseWindow();

        var (detailsOpen, setDetailsOpen) = UseState(false);
        var (nickname, setNickname) = UseState("");
        var (bio, setBio) = UseState("");
        var (busy, setBusy) = UseState(false);
        var (error, setError) = UseState<string?>(null);

        var (avatarSource, setAvatarSource) = UseState<ImageSource?>(null);
        var (bannerSource, setBannerSource) = UseState<ImageSource?>(null);
        var (pendingAvatar, setPendingAvatar) = UseState<byte[]?>(null);
        var (pendingAvatarType, setPendingAvatarType) = UseState<string?>(null);
        var (avatarPreview, setAvatarPreview) = UseState<ImageSource?>(null);
        var (removeAvatar, setRemoveAvatar) = UseState(false);

        var (pendingBanner, setPendingBanner) = UseState<byte[]?>(null);
        var (pendingBannerType, setPendingBannerType) = UseState<string?>(null);
        var (bannerPreview, setBannerPreview) = UseState<ImageSource?>(null);
        var (removeBanner, setRemoveBanner) = UseState(false);

        var session = state.Session;
        var profile = state.Profile;
        var avatarSha = profile?.Avatar?.Sha256;
        var bannerSha = profile?.Banner?.Sha256;

        UseEffect(
            () =>
            {
                if (avatarSha is not { Length: > 0 })
                {
                    setAvatarSource(null);
                    return () => { };
                }

                var cts = new CancellationTokenSource();
                _ = LoadAsync("avatar", avatarSha, cts.Token);
                return () =>
                {
                    cts.Cancel();
                    cts.Dispose();
                };

                async Task LoadAsync(string kind, string sha, CancellationToken ct)
                {
                    try
                    {
                        var payload = await gateway.GetMyImageAsync(session, kind, ct).ConfigureAwait(true);
                        var bitmap = await ProfileImageCache.LoadAsync(payload.Bytes, sha, ct).ConfigureAwait(true);
                        if (!ct.IsCancellationRequested) setAvatarSource(bitmap);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception)
                    {
                        if (!ct.IsCancellationRequested) setAvatarSource(null);
                    }
                }
            },
            avatarSha);

        UseEffect(
            () =>
            {
                if (bannerSha is not { Length: > 0 })
                {
                    setBannerSource(null);
                    return () => { };
                }

                var cts = new CancellationTokenSource();
                _ = LoadAsync("banner", bannerSha, cts.Token);
                return () =>
                {
                    cts.Cancel();
                    cts.Dispose();
                };

                async Task LoadAsync(string kind, string sha, CancellationToken ct)
                {
                    try
                    {
                        var payload = await gateway.GetMyImageAsync(session, kind, ct).ConfigureAwait(true);
                        var bitmap = await ProfileImageCache.LoadAsync(payload.Bytes, sha, ct).ConfigureAwait(true);
                        if (!ct.IsCancellationRequested) setBannerSource(bitmap);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception)
                    {
                        if (!ct.IsCancellationRequested) setBannerSource(null);
                    }
                }
            },
            bannerSha);

        var displayName = profile?.DisplayName(session.DisplayName) ?? session.DisplayName;
        var description = string.IsNullOrWhiteSpace(profile?.Bio)
            ? "还没有填写简介。点「编辑个人信息」写一句吧。"
            : profile!.Bio!;

        var shownAvatar = avatarPreview ?? (removeAvatar ? null : avatarSource);
        var shownBanner = bannerPreview ?? (removeBanner ? null : bannerSource);

        var cards = new List<Element>
        {
            Widgets.SettingCard(Widgets.GlyphIcon("\uE77B"), "用户名", session.DisplayName),
            Widgets.SettingCard(Widgets.GlyphIcon("\uE715"), "邮箱", session.DisplayEmail),
            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE8FB"),
                "邮箱状态",
                session.EmailVerified switch
                {
                    true => "已验证",
                    false => "未验证",
                    null => session.Email is null ? "未绑定" : "尚未确认",
                }),
            Widgets.SettingCard(Widgets.GlyphIcon("\uE823"), "登录状态", DescribeSignIn(session)),
            Widgets.SettingCard(
                Widgets.GlyphIcon("\uE7E8"),
                "退出登录",
                "回到登录页",
                trailing: Button("退出登录", () => dispatch(new AppAction.SignedOut())),
                actionToolTip: "退出登录"),
        };

        Element editor = ContentDialog(
            "编辑个人信息",
            VStack(
                12,
                TextBlock("头像").SemiBold(),
                HStack(
                    12,
                    Widgets.Avatar(shownAvatar, DefaultAvatarUri, DesignTokens.AvatarSize),
                    Button("更换", () => _ = PickImageSafelyAsync("avatar")),
                    Button("移除", () =>
                    {
                        setPendingAvatar(null);
                        setAvatarPreview(null);
                        setRemoveAvatar(true);
                    })),
                TextBlock("头图").SemiBold(),
                Border(
                    shownBanner is null
                        ? Border(Empty()).Height(96).Background(Theme.SubtleFill)
                        : Image("ms-appx:///Assets/Images/home-hero-banner-christmas-pink.jpg")
                            .Height(96)
                            .Set(image =>
                            {
                                image.Source = shownBanner;
                                image.Stretch = Stretch.UniformToFill;
                            })
                            .AccessibilityHidden())
                    .CornerRadius(4),
                HStack(
                    12,
                    Button("更换头图", () => _ = PickImageSafelyAsync("banner")),
                    Button("移除头图", () =>
                    {
                        setPendingBanner(null);
                        setBannerPreview(null);
                        setRemoveBanner(true);
                    })),
                DesignTokens.Field(
                    TextBox(nickname, setNickname, placeholderText: "例如：秋风").Header("昵称"),
                    "1–32 个字符，不能换行"),
                DesignTokens.Field(
                    TextBox(bio, setBio, placeholderText: "介绍一下自己")
                        .Header("简介")
                        .Height(120)
                        .Set(box =>
                        {
                            box.AcceptsReturn = true;
                            box.TextWrapping = TextWrapping.Wrap;
                        }),
                    "最多 500 个字符，纯文本"),
                error is null
                    ? null
                    : TextBlock(error)
                        .Foreground(Theme.SystemCritical)
                        .TextWrapping())
            .Width(520)) with
        {
            IsOpen = detailsOpen,
            PrimaryButtonText = busy ? "保存中…" : "保存",
            SecondaryButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = !busy,
            OnClosed = result =>
            {
                setDetailsOpen(false);
                if (result == ContentDialogResult.Primary)
                {
                    _ = SaveAsync();
                }
            },
        };

        return FlexColumn(
            Widgets.ProfileHeader(
                displayName,
                description,
                Widgets.Avatar(shownAvatar, DefaultAvatarUri, DesignTokens.AvatarSize),
                Button("编辑个人信息", OpenEditor),
                shownBanner),

            ScrollView((FlexColumn(
                VStack(4, [.. cards]).Margin(0, 16, 0, 0)) with
            {
                RowGap = 0,
            }).FlexPadding(DesignTokens.ContentPadding, 4, DesignTokens.ContentPadding, 40))
            .Flex(grow: 1, basis: 0),

            editor);

        void OpenEditor()
        {
            setNickname(profile?.Nickname ?? "");
            setBio(profile?.Bio ?? "");
            setPendingAvatar(null);
            setAvatarPreview(null);
            setRemoveAvatar(false);
            setPendingBanner(null);
            setBannerPreview(null);
            setRemoveBanner(false);
            setError(null);
            setDetailsOpen(true);
        }

        async Task PickImageSafelyAsync(string kind)
        {
            try
            {
                await PickImageAsync(kind);
            }
            catch (Exception)
            {
                setError("没能打开文件选择器，稍后再试。");
            }
        }

        async Task PickImageAsync(string kind)
        {
            // 选择器必须在 UI 线程、用当前窗口 HWND 打开；异常由外层 PickImageSafelyAsync 兜住。
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".webp");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window!.NativeWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            try
            {
                var limit = kind == "avatar" ? AvatarMaxBytes : BannerMaxBytes;
                var properties = await file.GetBasicPropertiesAsync();
                if (properties.Size > (ulong)limit)
                {
                    setError(kind == "avatar" ? "头像不能超过 5 MB。" : "头图不能超过 10 MB。");
                    return;
                }

                var bytes = await File.ReadAllBytesAsync(file.Path);
                if (bytes.LongLength > limit)
                {
                    setError(kind == "avatar" ? "头像不能超过 5 MB。" : "头图不能超过 10 MB。");
                    return;
                }

                var contentType = ContentTypeFor(file.FileType);
                var preview = await ProfileImageCache.LoadAsync(
                    bytes,
                    $"pending-{kind}-{Guid.NewGuid():N}",
                    CancellationToken.None);

                if (kind == "avatar")
                {
                    setPendingAvatar(bytes);
                    setPendingAvatarType(contentType);
                    setAvatarPreview(preview);
                    setRemoveAvatar(false);
                }
                else
                {
                    setPendingBanner(bytes);
                    setPendingBannerType(contentType);
                    setBannerPreview(preview);
                    setRemoveBanner(false);
                }

                setError(null);
            }
            catch (Exception)
            {
                setError("没能读取这张图片，换一张试试。");
            }
        }

        async Task SaveAsync()
        {
            if (nickname.Trim().Length == 0)
            {
                setError("昵称不能为空。");
                setDetailsOpen(true);
                return;
            }

            setBusy(true);
            setError(null);
            try
            {
                if (pendingAvatar is not null && pendingAvatarType is not null)
                {
                    await gateway.UploadMyImageAsync(session, "avatar", pendingAvatar, pendingAvatarType, CancellationToken.None);
                }
                else if (removeAvatar)
                {
                    await gateway.DeleteMyImageAsync(session, "avatar", CancellationToken.None);
                }

                if (pendingBanner is not null && pendingBannerType is not null)
                {
                    await gateway.UploadMyImageAsync(session, "banner", pendingBanner, pendingBannerType, CancellationToken.None);
                }
                else if (removeBanner)
                {
                    await gateway.DeleteMyImageAsync(session, "banner", CancellationToken.None);
                }

                var updated = await gateway.UpdateMyProfileAsync(
                    session,
                    nickname.Trim(),
                    string.IsNullOrWhiteSpace(bio) ? null : bio,
                    CancellationToken.None);

                dispatch(new AppAction.ProfileRefreshed(updated));
                setPendingAvatar(null);
                setPendingBanner(null);
                setAvatarPreview(null);
                setBannerPreview(null);
                setRemoveAvatar(false);
                setRemoveBanner(false);
                setDetailsOpen(false);
            }
            catch (EliCloudApiException ex)
            {
                setError(EliCloudErrorText.Describe(ex));
                setDetailsOpen(true);
                await RefreshAfterPartialSaveAsync();
            }
            catch (Exception)
            {
                setError(EliCloudErrorText.CannotReachServer);
                setDetailsOpen(true);
                await RefreshAfterPartialSaveAsync();
            }
            finally
            {
                setBusy(false);
            }

            async Task RefreshAfterPartialSaveAsync()
            {
                try
                {
                    var latest = await gateway.GetMyProfileAsync(session, CancellationToken.None);
                    dispatch(new AppAction.ProfileRefreshed(latest));
                }
                catch (Exception)
                {
                    // 连资料都拉不回来时，保留当前状态即可。
                }
            }
        }
    }

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };

    private static string DescribeSignIn(SessionState session) =>
        session.ExpiresAt is { } expiresAt
            ? session.IsAccessTokenExpired
                ? "已经过期，重新登录一下"
                : $"有效期到 {expiresAt.ToLocalTime():MM-dd HH:mm}"
            : "已登录";
}
