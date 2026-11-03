import AppKit
import SwiftUI

/// EliCloud macOS 客户端入口。
///
/// 窗口与菜单按 macOS 原生习惯：`NavigationSplitView` + 原生工具栏 + `Settings` scene，
/// 不套 Windows 式标题栏；尺寸按 Stage 4 §4.3（1252×800，最小 1000×640）。
@main
struct EliCloudApp: App {
    @StateObject private var app = AppState()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environmentObject(app)
                .frame(minWidth: 1000, minHeight: 640)
        }
        .defaultSize(width: 1252, height: 800)
        .windowResizability(.contentMinSize)
        .commands {
            CommandGroup(replacing: .appInfo) {
                Button(Copy.text("app.title")) {
                    NSApp.orderFrontStandardAboutPanel(nil)
                }
            }
            CommandGroup(replacing: .appSettings) {
                Button(Copy.text("nav.settings")) {
                    NSApp.sendAction(Selector(("showSettingsWindow:")), to: nil, from: nil)
                }
                .keyboardShortcut(",", modifiers: .command)
            }
            CommandGroup(after: .sidebar) {
                Button(Copy.text("common.refresh")) {
                    Task { await app.refreshAfterSignIn() }
                }
                .keyboardShortcut("r", modifiers: .command)
                .disabled(!app.isSignedIn)
            }
        }

        Settings {
            SettingsView()
                .environmentObject(app)
        }
    }
}

/// 未登录 → 登录流程；已登录 → 主壳。
struct RootView: View {
    @EnvironmentObject private var app: AppState

    var body: some View {
        Group {
            if app.isSignedIn {
                MainShellView()
            } else {
                AuthFlowView(app: app)
            }
        }
        .background(DesignTokens.colorSurfaceBase)
        .onAppear { app.applyAppearance() }
    }
}
