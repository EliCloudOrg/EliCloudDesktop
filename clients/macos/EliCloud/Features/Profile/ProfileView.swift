import EliCloudSDK
import SwiftUI
import UniformTypeIdentifiers

/// 个人资料页：页头（头像 / 昵称 / 简介 / 头图）+ 账号事实。
struct ProfileView: View {
    @EnvironmentObject private var app: AppState
    @State private var isEditing = false

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: DesignTokens.space6) {
                header
                accountFacts
            }
            .padding(DesignTokens.space6)
            .frame(maxWidth: 980, alignment: .leading)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .background(DesignTokens.colorSurfaceBase)
        .navigationTitle(Copy.text("nav.profile"))
        .toolbar {
            ToolbarItem(placement: .primaryAction) {
                Button(Copy.text("profile.edit.open")) {
                    isEditing = true
                }
                .accessibilityLabel(Copy.text("profile.edit.open"))
            }
        }
        .sheet(isPresented: $isEditing) {
            ProfileEditView()
                .environmentObject(app)
        }
        .task {
            await app.refreshProfile()
        }
    }

    // MARK: 页头

    private var header: some View {
        VStack(alignment: .leading, spacing: DesignTokens.space4) {
            if app.hasBanner, let image = ImageCache.shared.image(for: app.banner) {
                Image(nsImage: image)
                    .resizable()
                    .scaledToFill()
                    .frame(maxWidth: .infinity)
                    .aspectRatio(5, contentMode: .fit)
                    .clipped()
                    .clipShape(RoundedRectangle(cornerRadius: DesignTokens.radiusCard, style: .continuous))
            }

            HStack(alignment: .top, spacing: DesignTokens.space4) {
                AvatarView(data: app.avatar, size: 64)
                VStack(alignment: .leading, spacing: DesignTokens.space1) {
                    Text(app.displayName.isEmpty ? app.usernameLine : app.displayName)
                        .font(DesignTokens.typeTitle)
                        .foregroundStyle(DesignTokens.colorTextPrimary)
                    Text(bioLine)
                        .font(DesignTokens.typeBody)
                        .foregroundStyle(DesignTokens.colorTextSecondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                Spacer(minLength: 0)
            }
        }
    }

    private var bioLine: String {
        let bio = app.profile?.bio?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return bio.isEmpty ? Copy.text("profile.emptyBio") : bio
    }

    // MARK: 账号事实

    private var accountFacts: some View {
        Card {
            VStack(alignment: .leading, spacing: DesignTokens.space3) {
                Text(Copy.text("settings.account.title"))
                    .font(DesignTokens.typeBody)
                    .foregroundStyle(DesignTokens.colorTextPrimary)

                factRow(Copy.text("profile.username"), app.usernameLine.isEmpty ? "—" : app.usernameLine)
                factRow(Copy.text("profile.email"), app.emailLine.isEmpty ? Copy.text("profile.emailUnbound") : app.emailLine)
                factRow(
                    Copy.text("profile.emailStatus"),
                    app.emailVerified ? Copy.text("status.emailVerified") : Copy.text("status.emailUnverified")
                )
                factRow(Copy.text("profile.loginStatus"), Copy.text("status.loggedIn"))
            }
        }
    }

    private func factRow(_ label: String, _ value: String) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: DesignTokens.space4) {
            Text(label)
                .font(DesignTokens.typeCaption)
                .foregroundStyle(DesignTokens.colorTextSecondary)
                .frame(width: 96, alignment: .leading)
            Text(value)
                .font(DesignTokens.typeBody)
                .foregroundStyle(DesignTokens.colorTextPrimary)
                .textSelection(.enabled)
            Spacer(minLength: 0)
        }
        .accessibilityElement(children: .combine)
    }
}

/// 编辑个人信息 sheet：昵称/简介 + 头像/头图（选择 → 裁切 → 上传）。
struct ProfileEditView: View {
    @EnvironmentObject private var app: AppState
    @Environment(\.dismiss) private var dismiss

    @State private var nickname = ""
    @State private var bio = ""
    @State private var pendingAvatar: Data?
    @State private var pendingBanner: Data?
    @State private var cropTarget: CropTarget?
    @State private var importerKind: ImageKind?
    @State private var isImporting = false
    @State private var isSaving = false
    @State private var message: String?

    enum ImageKind: String, Identifiable {
        case avatar
        case banner
        var id: String { rawValue }
    }

    struct CropTarget: Identifiable {
        let id = UUID()
        let kind: ImageKind
        let image: CGImage
    }

    var body: some View {
        VStack(alignment: .leading, spacing: DesignTokens.space4) {
            Text(Copy.text("profile.edit.title"))
                .font(DesignTokens.typeTitle)

            ScrollView {
                VStack(alignment: .leading, spacing: DesignTokens.space4) {
                    avatarSection
                    bannerSection

                    VStack(alignment: .leading, spacing: DesignTokens.space1) {
                        Text(Copy.text("profile.edit.nickname"))
                            .font(DesignTokens.typeCaption)
                            .foregroundStyle(DesignTokens.colorTextSecondary)
                        TextField("", text: $nickname, prompt: Text(Copy.text("profile.edit.nicknamePlaceholder")))
                            .textFieldStyle(.roundedBorder)
                            .accessibilityLabel(Copy.text("profile.edit.nickname"))
                        FieldHint(text: Copy.text("profile.edit.nicknameHint"))
                    }

                    VStack(alignment: .leading, spacing: DesignTokens.space1) {
                        Text(Copy.text("profile.edit.bio"))
                            .font(DesignTokens.typeCaption)
                            .foregroundStyle(DesignTokens.colorTextSecondary)
                        TextEditor(text: $bio)
                            .font(DesignTokens.typeBody)
                            .frame(minHeight: 88)
                            .padding(DesignTokens.space1)
                            .overlay(
                                RoundedRectangle(cornerRadius: DesignTokens.radiusControl, style: .continuous)
                                    .strokeBorder(DesignTokens.colorStrokeDefault, lineWidth: 1)
                            )
                            .accessibilityLabel(Copy.text("profile.edit.bio"))
                        FieldHint(text: Copy.text("profile.edit.bioHint"))
                    }

                    if let message {
                        InlineError(message: message)
                    }
                }
            }

            HStack {
                Spacer()
                Button(Copy.text("common.cancel")) { dismiss() }
                    .keyboardShortcut(.cancelAction)
                Button(isSaving ? Copy.text("common.saving") : Copy.text("common.save")) {
                    Task { await save() }
                }
                .buttonStyle(.borderedProminent)
                .keyboardShortcut(.defaultAction)
                .disabled(isSaving || !isValid)
            }
        }
        .padding(DesignTokens.space6)
        .frame(minWidth: 520, idealWidth: 560, minHeight: 560)
        .onAppear(perform: loadInitialValues)
        .fileImporter(
            isPresented: $isImporting,
            allowedContentTypes: [.png, .jpeg, .webP],
            allowsMultipleSelection: false
        ) { result in
            handleImport(result)
        }
        .sheet(item: $cropTarget) { target in
            CropSheet(target: target) { pngData in
                switch target.kind {
                case .avatar: pendingAvatar = pngData
                case .banner: pendingBanner = pngData
                }
                cropTarget = nil
            } onCancel: {
                cropTarget = nil
            }
            .environmentObject(app)
        }
    }

    // MARK: 区块

    private var avatarSection: some View {
        HStack(spacing: DesignTokens.space4) {
            AvatarView(data: pendingAvatar ?? app.avatar, size: 64)
            VStack(alignment: .leading, spacing: DesignTokens.space1) {
                Text(Copy.text("profile.edit.avatar"))
                    .font(DesignTokens.typeBody)
                Button(Copy.text("profile.edit.change")) {
                    importerKind = .avatar
                    isImporting = true
                }
                .accessibilityLabel(Copy.text("profile.edit.avatar"))
            }
            Spacer(minLength: 0)
        }
    }

    private var bannerSection: some View {
        VStack(alignment: .leading, spacing: DesignTokens.space2) {
            Text(Copy.text("profile.edit.banner"))
                .font(DesignTokens.typeBody)
            if let data = pendingBanner ?? app.banner, let image = ImageCache.shared.image(for: data) {
                Image(nsImage: image)
                    .resizable()
                    .scaledToFill()
                    .frame(height: 96)
                    .frame(maxWidth: .infinity)
                    .clipped()
                    .clipShape(RoundedRectangle(cornerRadius: DesignTokens.radiusControl, style: .continuous))
            }
            HStack(spacing: DesignTokens.space3) {
                Button(Copy.text("profile.edit.changeBanner")) {
                    importerKind = .banner
                    isImporting = true
                }
                Button(Copy.text("profile.edit.removeBanner")) {
                    pendingBanner = Data()
                }
                .disabled(app.banner == nil && pendingBanner == nil)
            }
        }
    }

    // MARK: 数据流

    private var isValid: Bool {
        (try? EliCloudValidation.normalizeNickname(nickname)) != nil
            && (try? EliCloudValidation.normalizeBio(bio)) != nil
    }

    private func loadInitialValues() {
        nickname = app.profile?.nickname ?? ""
        bio = app.profile?.bio ?? ""
    }

    private func handleImport(_ result: Result<[URL], Error>) {
        guard let kind = importerKind else { return }
        switch result {
        case .failure:
            message = Copy.text("profile.edit.pickerFailed")
        case .success(let urls):
            guard let url = urls.first else { return }
            do {
                let data = try readSecurityScopedData(url)
                guard ImagePipeline.isSupportedImage(data) else {
                    message = Copy.text("error.profile.unsupported_image_type")
                    return
                }
                let limit = kind == .avatar
                    ? EliCloudValidation.avatarMaxBytes
                    : EliCloudValidation.bannerMaxBytes
                guard data.count <= limit else {
                    message = kind == .avatar
                        ? Copy.text("profile.edit.avatarTooLarge")
                        : Copy.text("profile.edit.bannerTooLarge")
                    return
                }
                let image = try ImagePipeline.loadImage(from: data)
                cropTarget = CropTarget(kind: kind, image: image)
                message = nil
            } catch {
                message = Copy.text("profile.edit.imageReadFailed")
            }
        }
    }

    private func readSecurityScopedData(_ url: URL) throws -> Data {
        let scoped = url.startAccessingSecurityScopedResource()
        defer {
            if scoped { url.stopAccessingSecurityScopedResource() }
        }
        return try Data(contentsOf: url)
    }

    /// 保存顺序：先文字，再图片；部分失败时刷新服务端快照，只丢弃失败的部分。
    private func save() async {
        let client = app.client
        isSaving = true
        message = nil
        defer { isSaving = false }

        do {
            let updated = try await client.userInfo.updateProfile(nickname: nickname, bio: bio.isEmpty ? nil : bio)
            app.adoptProfile(updated)
        } catch {
            message = Copy.error(EliCloudError.normalized(error))
            return
        }

        var failure: String?
        if let avatar = pendingAvatar {
            do {
                let profile = try await client.userInfo.uploadImage(kind: .avatar, pngData: avatar)
                app.adoptProfile(profile)
                pendingAvatar = nil
            } catch {
                failure = Copy.error(EliCloudError.normalized(error))
            }
        }
        if let banner = pendingBanner {
            if banner.isEmpty {
                do {
                    _ = try await client.userInfo.deleteImage(kind: .banner)
                    pendingBanner = nil
                } catch {
                    failure = Copy.error(EliCloudError.normalized(error))
                }
            } else {
                do {
                    let profile = try await client.userInfo.uploadImage(kind: .banner, pngData: banner)
                    app.adoptProfile(profile)
                    pendingBanner = nil
                } catch {
                    failure = Copy.error(EliCloudError.normalized(error))
                }
            }
        }

        // 部分成功：以服务端快照为准。
        await app.refreshProfile()
        if let failure {
            message = failure
        } else {
            dismiss()
        }
    }
}

/// 裁切 sheet：选好图后进入这里，应用后回传 PNG。
struct CropSheet: View {
    let target: ProfileEditView.CropTarget
    let onApply: (Data) -> Void
    let onCancel: () -> Void

    @StateObject private var model: CropModel
    @State private var message: String?

    init(
        target: ProfileEditView.CropTarget,
        onApply: @escaping (Data) -> Void,
        onCancel: @escaping () -> Void
    ) {
        self.target = target
        self.onApply = onApply
        self.onCancel = onCancel
        _model = StateObject(
            wrappedValue: CropModel(
                source: target.image,
                aspect: target.kind == .avatar ? 1 : 5
            )
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: DesignTokens.space3) {
            CropView(
                model: model,
                title: Copy.text(target.kind == .avatar ? "profile.edit.cropAvatar" : "profile.edit.cropBanner"),
                hint: Copy.text(target.kind == .avatar ? "profile.edit.cropAvatarHint" : "profile.edit.cropBannerHint"),
                onCancel: onCancel,
                onApply: apply
            )
            if let message {
                InlineError(message: message)
                    .padding(.horizontal, DesignTokens.space6)
                    .padding(.bottom, DesignTokens.space4)
            }
        }
    }

    private func apply() {
        do {
            let maxDimension = target.kind == .avatar
                ? ImagePipeline.avatarMaxDimension
                : ImagePipeline.bannerMaxDimension
            let data = try ImagePipeline.pngData(
                cropped: target.image,
                normalizedRect: model.rect,
                maxDimension: maxDimension
            )
            onApply(data)
        } catch {
            message = Copy.text("profile.edit.cropFailed")
        }
    }
}
