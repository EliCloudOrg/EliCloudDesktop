import AppKit
import EliCloudSDK
import Foundation
import SwiftUI

/// 外观偏好（设置页与菜单共用；只存主题，不存任何凭据）。
enum ThemePreference: String, CaseIterable, Identifiable {
    case system
    case light
    case dark

    var id: String { rawValue }

    var copyKey: String { "settings.appearance.\(rawValue)" }

    var appearance: NSAppearance? {
        switch self {
        case .system: return nil
        case .light: return NSAppearance(named: .aqua)
        case .dark: return NSAppearance(named: .darkAqua)
        }
    }
}

/// 服务清单（当前硬编码；`/core/v1/services` 未上线，见契约 §2.4）。
enum ServiceID: String, CaseIterable, Identifiable {
    case mc
    case pdf

    var id: String { rawValue }

    var titleKey: String { "service.\(rawValue).title" }
    var detailKey: String { "service.\(rawValue).description" }

    var isImplemented: Bool { self == .mc }
}

enum SidebarItem: Hashable {
    case home
    case profile
    case service(ServiceID)
    case settings
}

/// 应用状态：内存会话 + 当前资料 + 服务探活结果 + 外观。
///
/// 令牌只存在 `EliCloudSDK.SessionStore`（actor、内存）；这里只读快照，不落盘。
@MainActor
final class AppState: ObservableObject {

    @Published private(set) var session: SessionState?
    @Published private(set) var profile: Components.Schemas.UserProfile?
    @Published private(set) var avatar: Data?
    @Published private(set) var banner: Data?
    @Published private(set) var mcHealth: Components.Schemas.McHealthStatus?
    @Published private(set) var mcNames: Components.Schemas.McNameListResponse?
    @Published private(set) var isProbing = false
    @Published var selection: SidebarItem? = .home
    @Published var theme: ThemePreference {
        didSet {
            UserDefaults.standard.set(theme.rawValue, forKey: Self.themeDefaultsKey)
            applyAppearance()
        }
    }

    let client: EliCloudClient
    private static let themeDefaultsKey = "appearance"

    init(baseURL: URL? = nil, userAgent: String = BuildConfig.userAgent) {
        let url = baseURL ?? URL(string: BuildConfig.baseAddress) ?? URL(string: "https://api.example.com")!
        self.client = EliCloudClient(baseURL: url, userAgent: userAgent)
        let stored = UserDefaults.standard.string(forKey: Self.themeDefaultsKey) ?? ThemePreference.system.rawValue
        self.theme = ThemePreference(rawValue: stored) ?? .system
    }

    // MARK: 派生状态

    var isSignedIn: Bool { session != nil }

    var displayName: String {
        let nickname = profile?.nickname ?? ""
        if !nickname.isEmpty { return nickname }
        return session?.username ?? ""
    }

    var usernameLine: String { session?.username ?? "" }
    var emailLine: String { session?.email ?? "" }
    var emailVerified: Bool { session?.emailVerified ?? false }

    var hasBanner: Bool {
        !(banner ?? Data()).isEmpty
    }

    // MARK: 外观

    func applyAppearance() {
        NSApp?.appearance = theme.appearance
    }

    // MARK: 会话

    /// 登录 + 拉身份（身份失败不影响登录结果，契约 §5）。
    func signIn(username: String, password: String) async throws {
        try await client.sso.loginAndLoadIdentity(username: username, password: password)
        await refreshAfterSignIn()
    }

    func signOut() async {
        await client.sso.signOut()
        session = nil
        profile = nil
        avatar = nil
        banner = nil
        mcHealth = nil
        mcNames = nil
        selection = .home
    }

    /// 登录后刷新：会话快照 + 资料 + 图片 + 服务探活。
    func refreshAfterSignIn() async {
        session = await client.sessionStore.current
        await probeServices()
        await refreshProfile()
    }

    func refreshProfile() async {
        guard isSignedIn else { return }
        profile = try? await client.userInfo.myProfile()
        // `try?` 对返回可选值的函数会拍平，这里拿到的就是 ProfileImage?。
        if let image = try? await client.userInfo.myImage(kind: .avatar) {
            avatar = image.data
        } else {
            avatar = nil
        }
        if let image = try? await client.userInfo.myImage(kind: .banner) {
            banner = image.data
        } else {
            banner = nil
        }
    }

    // MARK: 服务探活

    /// 探活：MC 的 200/503 都算“有结果”，只有其它错误才算探活失败。
    func probeServices() async {
        isProbing = true
        defer { isProbing = false }
        mcHealth = try? await client.mc.health()
    }

    // MARK: MC

    func loadMcNames(includeRemoved: Bool = false) async throws {
        mcNames = try await client.mc.names(includeRemoved: includeRemoved)
    }

    // MARK: 资料编辑后的同步

    func adoptProfile(_ updated: Components.Schemas.UserProfile) {
        profile = updated
    }

    func adoptAvatar(_ data: Data?) {
        avatar = data
    }

    func adoptBanner(_ data: Data?) {
        banner = data
    }
}
