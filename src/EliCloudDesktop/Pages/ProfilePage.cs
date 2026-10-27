using System.Globalization;
using System.Text;
using CommunityToolkit.WinUI.Controls;
using EliCloud.Sdk;
using EliCloudDesktop.Core;
using EliCloudDesktop.Services;
using EliCloudDesktop.Ui;
using Microsoft.UI.Reactor;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using static EliCloudDesktop.Ui.ImageCropperElement;
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
/// 弹窗取消/关闭时则丢弃全部未提交的预览：页头只反映服务端已有的资料，
/// 不能把「选好但没保存」的图当成已经保存了。
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

    private const string AvatarKind = "avatar";
    private const string BannerKind = "banner";

    /// <summary>昵称长度上限，与 userinfo 的 <c>max_nickname_chars</c> 一致。</summary>
    private const int NicknameMaxChars = 32;

    /// <summary>
    /// 头图裁切比例（宽:高）。页头横幅按 <see cref="DesignTokens.BannerAspectRatio"/> 等比缩放，
    /// 按同一个比例裁，用户在这里看到的取景就是页头上的取景，不会被 UniformToFill 再切一刀。
    /// </summary>
    private const double BannerCropAspectRatio = DesignTokens.BannerAspectRatio;

    /// <summary>弹窗内容宽度。ContentDialog 的内容区约 498，超过它右侧会被裁掉一条。</summary>
    private const double DialogContentWidth = 480;

    private const double CropperWidth = DialogContentWidth;
    private const double CropperHeight = 320;

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

        // 裁切步骤和编辑表单共用同一个 ContentDialog：cropOpen 只切换「里面显示什么」，
        // 弹窗本身不关 —— 同一时刻开第二个 ContentDialog 会被 WinUI 拒绝。
        var (cropOpen, setCropOpen) = UseState(false);
        var (cropKind, setCropKind) = UseState(AvatarKind);
        var (cropSource, setCropSource) = UseState<WriteableBitmap?>(null);
        var (cropReady, setCropReady) = UseState(false);
        var (cropping, setCropping) = UseState(false);
        var (cropError, setCropError) = UseState<string?>(null);

        // ImageCropper 的工厂元素拿不到原生实例，走 .Set 逃生舱存一份，应用裁切时需要它调 SaveAsync。
        var cropNative = UseRef<ImageCropper?>(null);

        // ContentDialog 同理：主题要在它真正打开之后写给原生控件（见 ApplyDialogTheme）。
        var editorDialog = UseRef<ContentDialog?>(null);

        // 裁切步骤取消后要把编辑表单重新打开。Reactor 按值 diff IsOpen，必须在「关闭」已经
        // 落成一次渲染之后再翻回 true，否则 true→true 不会重新 Show —— 用 effect 等这一拍。
        var (reopenEditor, bumpReopenEditor) = UseReducer(0);

        var (pendingBanner, setPendingBanner) = UseState<byte[]?>(null);
        var (pendingBannerType, setPendingBannerType) = UseState<string?>(null);
        var (bannerPreview, setBannerPreview) = UseState<ImageSource?>(null);
        var (removeBanner, setRemoveBanner) = UseState(false);

        // 页头实际宽度：承托面按内容收缩，长简介需要一个「能长到多宽」的上限，
        // 而上限只能来自页头自己量到的宽度（窗口/导航栏变化都会改它）。
        var (headerWidth, setHeaderWidth) = UseState(DesignTokens.WindowWidth - DesignTokens.NavPaneWidth);

        // 「编辑个人信息」按钮的实测宽度：标题需要给它让出位置才知道自己能占多宽。
        var (actionWidth, setActionWidth) = UseState(112d);

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

        // 关掉弹窗之后重新打开编辑表单：effect 跑在「关闭」这一帧提交之后，
        // 此时 IsOpen 已经是 false，再置 true 就是一次真正的 false→true，Reactor 会重新 Show。
        UseEffect(
            () =>
            {
                if (reopenEditor == 0) return () => { };
                setDetailsOpen(true);
                return () => { };
            },
            reopenEditor);

        var displayName = profile?.DisplayName(session.DisplayName) ?? session.DisplayName;
        var description = string.IsNullOrWhiteSpace(profile?.Bio)
            ? "还没有填写简介。点「编辑个人信息」写一句吧。"
            : profile!.Bio!;

        var shownAvatar = avatarPreview ?? avatarSource;
        var shownBanner = bannerPreview ?? (removeBanner ? null : bannerSource);

        // 新账号还没有昵称时，编辑框默认填用户名：页头/Nav 一直用用户名兜底，
        // 这里让「默认昵称」在编辑表单里也成立，用户不必先补一份重复的信息。
        var defaultNickname = string.IsNullOrWhiteSpace(profile?.Nickname)
            ? session.Username ?? string.Empty
            : profile!.Nickname!;
        var nicknameIsValid = IsValidNickname(nickname);

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
        };

        Element editor = (ContentDialog(
            cropOpen ? (cropKind == BannerKind ? "裁切头图" : "裁切头像") : "编辑个人信息",
            cropOpen ? BuildCropStep() : BuildEditor())
        with
        {
            IsOpen = detailsOpen,
            PrimaryButtonText = cropOpen ? "应用裁切" : busy ? "保存中…" : "保存",
            SecondaryButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = cropOpen ? cropReady && !cropping : !busy && nicknameIsValid,
            OnOpened = ApplyDialogTheme,
            OnClosed = result =>
            {
                setDetailsOpen(false);

                if (cropOpen)
                {
                    if (result == ContentDialogResult.Primary)
                    {
                        // 裁切结果在弹窗关闭后取：ImageCropper 还在树里，SaveAsync 用的也是上一次
                        // 布局算出的选区，不需要重新显示。
                        _ = ApplyCropAsync();
                    }
                    else
                    {
                        // 取消裁切：回到编辑表单，别连已经填好的资料一起丢。
                        CancelCrop();
                        ReopenEditor();
                    }
                    return;
                }

                if (result == ContentDialogResult.Primary)
                {
                    _ = SaveAsync();
                    return;
                }

                // 取消 / 关闭（含 Esc 和右上角 ×）不是保存：把弹窗里改过但没提交的东西全部丢掉。
                // 这些状态会直接参与页头和下一次编辑的显示，留着就会出现「取消后页头显示了一张
                // 其实没保存的图，再次打开编辑又消失」的假象。
                DiscardPendingEdits();
            },
        })
        // 元素本身没有 RequestedTheme，先把原生控件存下来，等 Opened 之后再写。
        .Set(dialog => editorDialog.Current = dialog);

        // 标题的可收缩宽度要给动作按钮让位，按钮宽度只能从原生控件量（字号缩放会改它）。
        var editAction = Button("编辑个人信息", OpenEditor)
            .OnSizeChanged((_, args) =>
            {
                var width = Math.Round(args.NewSize.Width);
                if (Math.Abs(width - actionWidth) >= 1) setActionWidth(width);
            });

        return FlexColumn(
            Widgets.ProfileHeader(
                displayName,
                description,
                Widgets.Avatar(shownAvatar, DefaultAvatarUri, DesignTokens.AvatarSize),
                editAction,
                shownBanner,
                headerWidth,
                actionWidth)
                .OnSizeChanged((_, args) =>
                {
                    // 只在宽度真的变了才写状态：高度变化（简介换行）不需要重算文字上限。
                    var width = Math.Round(args.NewSize.Width);
                    if (Math.Abs(width - headerWidth) >= 1) setHeaderWidth(width);
                }),

            ScrollView((FlexColumn(
                VStack(4, [.. cards]).Margin(0, 16, 0, 0)) with
            {
                RowGap = 0,
            }).FlexPadding(DesignTokens.ContentPadding, 4, DesignTokens.ContentPadding, 40))
            .Flex(grow: 1, basis: 0),

            editor);

        Element BuildEditor() => VStack(
            12,
            TextBlock("头像").SemiBold(),
            HStack(
                12,
                Widgets.Avatar(shownAvatar, DefaultAvatarUri, DesignTokens.AvatarSize),
                Button("更换", () => _ = PickImageSafelyAsync(AvatarKind))),
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
                Button("更换头图", () => _ = PickImageSafelyAsync(BannerKind)),
                Button("移除头图", () =>
                {
                    setPendingBanner(null);
                    setBannerPreview(null);
                    setRemoveBanner(true);
                })),
            DesignTokens.Field(
                TextBox(nickname, setNickname, placeholderText: "例如：秋风").Header("昵称"),
                "默认使用用户名；1–32 个字符，不能换行"),
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
        .Width(DialogContentWidth);

        Element BuildCropStep()
        {
            var isBanner = cropKind == BannerKind;
            var cropper = isBanner
                ? ImageCropper(cropShape: CropShape.Rectangular, aspectRatio: BannerCropAspectRatio)
                : ImageCropper(cropShape: CropShape.Circular);
            if (cropSource is not null)
            {
                cropper = cropper with { Source = cropSource };
            }

            return VStack(
                12,
                DesignTokens.Body(
                        isBanner
                            ? "拖动选区，或用四角手柄调整大小；头图按页头横幅的比例（5:1）裁切。"
                            : "拖动选区，或用四角手柄调整大小；头像会按正方形裁切。")
                    .Foreground(Theme.SecondaryText)
                    .TextWrapping(),
                cropper
                    .Width(CropperWidth)
                    .Height(CropperHeight)
                    // 控件本身撑满内容宽度、自己画底和圆角，不再套一层带背景的 Border ——
                    // 那层背景一旦比弹窗内容宽，右侧会被裁掉、只剩左边一条色带。
                    .Set(native =>
                    {
                        // 元素本身拿不到原生控件，存一份给 SaveAsync 用。
                        cropNative.Current = native;

                        // 模板默认 Padding=80，会把内部画布挤到控件的一半大，图也跟着缩。
                        // 16 刚好够 24px 的手柄露在选区外。
                        native.Padding = new Thickness(16);
                        native.CornerRadius = new CornerRadius(8);
                    }),
                cropError is null
                    ? cropReady
                        ? null
                        : DesignTokens.Hint("正在读取图片…")
                    : TextBlock(cropError).Foreground(Theme.SystemCritical).TextWrapping())
            .Width(DialogContentWidth);
        }

        void OpenEditor()
        {
            setNickname(defaultNickname);
            setBio(profile?.Bio ?? "");
            DiscardPendingEdits();
            setCropOpen(false);
            setCropKind(AvatarKind);
            setCropSource(null);
            setCropReady(false);
            setCropping(false);
            setCropError(null);
            setDetailsOpen(true);
        }

        void DiscardPendingEdits()
        {
            setPendingAvatar(null);
            setPendingAvatarType(null);
            setAvatarPreview(null);
            setPendingBanner(null);
            setPendingBannerType(null);
            setBannerPreview(null);
            setRemoveBanner(false);
            setError(null);
        }

        // 图片一旦写进 userinfo，就不再是「待保存」的了：清掉待上传的字节，
        // 但预览先留着 —— 刷新回来的位图就绪之前，弹窗里不能先空一块。
        void CommitPendingAvatar()
        {
            setPendingAvatar(null);
            setPendingAvatarType(null);
        }

        void CommitPendingBanner()
        {
            setPendingBanner(null);
            setPendingBannerType(null);
            setRemoveBanner(false);
        }

        // 弹窗已经以 Primary 关闭时，Reactor 这一拍看到的仍是 IsOpen=true；同一次提交里再
        // setDetailsOpen(true) 会被 diff 成 true→true 而不会重新 Show。统一走这个计数器，
        // 让 effect 等「关闭」那一帧落定之后再把弹窗打开。
        void ReopenEditor() => bumpReopenEditor(n => n + 1);

        void CancelCrop()
        {
            setCropOpen(false);
            setCropSource(null);
            setCropReady(false);
            setCropError(null);
        }

        void ApplyDialogTheme()
        {
            // 弹窗挂在 XamlRoot 浮层上，不继承应用根节点的 RequestedTheme；而且必须等它真正
            // 打开（Opened）之后再写给原生控件 —— 在挂载时写会被随后的弹窗模板/主题解析覆盖。
            var dialog = editorDialog.Current;
            if (dialog is null) return;

            var theme = state.Theme.ToElementTheme();
            if (dialog.RequestedTheme != theme) dialog.RequestedTheme = theme;
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
                var isAvatar = kind == AvatarKind;
                var limit = isAvatar ? AvatarMaxBytes : BannerMaxBytes;
                var properties = await file.GetBasicPropertiesAsync();
                if (properties.Size > (ulong)limit)
                {
                    setError(isAvatar ? "头像不能超过 5 MB。" : "头图不能超过 10 MB。");
                    return;
                }

                var raw = await File.ReadAllBytesAsync(file.Path);
                if (raw.LongLength > limit)
                {
                    setError(isAvatar ? "头像不能超过 5 MB。" : "头图不能超过 10 MB。");
                    return;
                }

                // 头像和头图都先裁再定：解成 ImageCropper 要的 WriteableBitmap，
                // 最终字节要等用户确认裁切后才产生。
                setCropKind(kind);
                setCropSource(await ProfileImageProcessing.LoadCropSourceAsync(raw));
                setCropReady(true);
                setCropError(null);
                setCropOpen(true);
                setError(null);
            }
            catch (Exception)
            {
                setError("没能读取这张图片，换一张试试。");
            }
        }

        async Task ApplyCropAsync()
        {
            if (!cropReady || cropping) return;

            setCropping(true);
            setCropError(null);
            try
            {
                var cropper = cropNative.Current;
                if (cropper?.Source is null)
                {
                    throw new InvalidOperationException("图片还没加载完。");
                }

                using var stream = new InMemoryRandomAccessStream();
                await cropper.SaveAsync(stream, BitmapFileFormat.Png, keepRectangularOutput: true);
                var cropped = await ProfileImageProcessing.ReadAllBytesAsync(stream);

                if (cropKind == BannerKind)
                {
                    var banner = await ProfileImageProcessing.FitAsync(
                        cropped, ProfileImageProcessing.BannerMaxEdge);
                    var preview = await ProfileImageCache.LoadAsync(
                        banner,
                        $"pending-banner-{Guid.NewGuid():N}",
                        CancellationToken.None);

                    setPendingBanner(banner);
                    setPendingBannerType("image/png");
                    setBannerPreview(preview);
                    setRemoveBanner(false);
                }
                else
                {
                    var avatar = await ProfileImageProcessing.FitAsync(
                        cropped, ProfileImageProcessing.AvatarMaxEdge);
                    var preview = await ProfileImageCache.LoadAsync(
                        avatar,
                        $"pending-avatar-{Guid.NewGuid():N}",
                        CancellationToken.None);

                    setPendingAvatar(avatar);
                    setPendingAvatarType("image/png");
                    setAvatarPreview(preview);
                }

                setCropOpen(false);
                setCropSource(null);
                setCropReady(false);
                setError(null);
                ReopenEditor();
            }
            catch (Exception)
            {
                setCropError("没能完成裁切，换一张图片再试。");
                // 弹窗已被主按钮关掉，这里重新打开，停在裁切步骤让用户重试。
                ReopenEditor();
            }
            finally
            {
                setCropping(false);
            }
        }

        async Task SaveAsync()
        {
            // 主按钮在昵称不合法时就是禁用的；这里只防御状态竞态，不补校验提示。
            // 万一还是走到这里，把弹窗重新打开，别让刚选的图和文字一起被吞掉。
            if (!IsValidNickname(nickname))
            {
                ReopenEditor();
                return;
            }

            setBusy(true);
            setError(null);

            // 图片是否已经真的写进服务端：写成功的那一份要停止当作「待保存」，
            // 后面的文字保存失败或网络失败都不该让它重新变回草稿。
            var avatarSaved = false;
            var bannerSaved = false;

            try
            {
                // 「图片先、文字后」：先把头像/头图写掉，再保存文字。
                // 这样文字那一步失败时，已经换好的图仍然落库，不会随着弹窗一起丢掉。
                if (pendingAvatar is not null && pendingAvatarType is not null)
                {
                    await gateway.UploadMyImageAsync(session, "avatar", pendingAvatar, pendingAvatarType, CancellationToken.None);
                    avatarSaved = true;
                }

                if (pendingBanner is not null && pendingBannerType is not null)
                {
                    await gateway.UploadMyImageAsync(session, "banner", pendingBanner, pendingBannerType, CancellationToken.None);
                    bannerSaved = true;
                }
                else if (removeBanner)
                {
                    await gateway.DeleteMyImageAsync(session, "banner", CancellationToken.None);
                    bannerSaved = true;
                }

                var updated = await gateway.UpdateMyProfileAsync(
                    session,
                    nickname.Trim(),
                    string.IsNullOrWhiteSpace(bio) ? null : bio,
                    CancellationToken.None);

                dispatch(new AppAction.ProfileRefreshed(updated));
                DiscardPendingEdits();
                setDetailsOpen(false);
            }
            catch (EliCloudApiException ex)
            {
                setError(EliCloudErrorText.Describe(ex));
                ReopenEditor();
                var refreshed = await RefreshAfterPartialSaveAsync();
                if (refreshed && avatarSaved) CommitPendingAvatar();
                if (refreshed && bannerSaved) CommitPendingBanner();
            }
            catch (Exception)
            {
                setError(EliCloudErrorText.CannotReachServer);
                ReopenEditor();
                var refreshed = await RefreshAfterPartialSaveAsync();
                if (refreshed && avatarSaved) CommitPendingAvatar();
                if (refreshed && bannerSaved) CommitPendingBanner();
            }
            finally
            {
                setBusy(false);
            }

            async Task<bool> RefreshAfterPartialSaveAsync()
            {
                try
                {
                    var latest = await gateway.GetMyProfileAsync(session, CancellationToken.None);
                    dispatch(new AppAction.ProfileRefreshed(latest));
                    return true;
                }
                catch (Exception)
                {
                    // 连资料都拉不回来时，保留当前状态即可；待保存的预览也先留着，
                    // 下一次保存还能把它重传一遍。
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// 昵称是否可以直接提交。规则与 userinfo 的 <c>normalize_nickname</c> 对齐：
    /// 去掉首尾空白、NFC 归一化后是 1–32 个码点，且不含控制/格式字符。
    /// </summary>
    private static bool IsValidNickname(string value)
    {
        var cleaned = value.Trim().Normalize(NormalizationForm.FormC);
        if (cleaned.Length == 0) return false;

        var count = 0;
        foreach (var rune in cleaned.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse
                or UnicodeCategory.OtherNotAssigned)
            {
                return false;
            }

            count++;
        }

        return count <= NicknameMaxChars;
    }

    private static string DescribeSignIn(SessionState session) =>
        session.ExpiresAt is { } expiresAt
            ? session.IsAccessTokenExpired
                ? "已经过期，重新登录一下"
                : $"有效期到 {expiresAt.ToLocalTime():MM-dd HH:mm}"
            : "已登录";
}
