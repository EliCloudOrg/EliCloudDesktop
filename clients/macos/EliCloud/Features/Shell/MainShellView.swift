import SwiftUI

/// 主壳：原生 `NavigationSplitView`（Sidebar 220–260）+ 原生工具栏。
struct MainShellView: View {
    @EnvironmentObject private var app: AppState

    var body: some View {
        NavigationSplitView {
            SidebarView()
                .navigationSplitViewColumnWidth(min: 220, ideal: 240, max: 260)
        } detail: {
            DetailView(item: app.selection ?? .home)
        }
        .toolbar {
            ToolbarItem(placement: .navigation) {
                Button {
                    Task { await app.refreshAfterSignIn() }
                } label: {
                    Label(Copy.text("common.refresh"), systemImage: "arrow.clockwise")
                }
                .help(Copy.text("common.refresh"))
                .disabled(app.isProbing)
            }
        }
    }
}

/// 侧栏：主页 / 服务列表（动态）/ 设置 + 页脚个人资料。
struct SidebarView: View {
    @EnvironmentObject private var app: AppState

    var body: some View {
        List(selection: $app.selection) {
            Section {
                Label(Copy.text("nav.home"), systemImage: "house")
                    .tag(SidebarItem.home)
            }

            Section {
                ForEach(ServiceID.allCases) { service in
                    Label(Copy.text(service.titleKey), systemImage: symbol(for: service))
                        .tag(SidebarItem.service(service))
                }
            } header: {
                Text(Copy.text("nav.services"))
            }

            Section {
                Label(Copy.text("nav.settings"), systemImage: "gearshape")
                    .tag(SidebarItem.settings)
            }
        }
        .listStyle(.sidebar)
        .safeAreaInset(edge: .bottom) {
            SidebarFooter()
        }
    }

    private func symbol(for service: ServiceID) -> String {
        switch service {
        case .mc: return "cube.box"
        case .pdf: return "doc.richtext"
        }
    }
}

private struct SidebarFooter: View {
    @EnvironmentObject private var app: AppState

    var body: some View {
        Button {
            app.selection = .profile
        } label: {
            HStack(spacing: DesignTokens.space2) {
                AvatarView(data: app.avatar, size: 32)
                VStack(alignment: .leading, spacing: 0) {
                    Text(app.displayName.isEmpty ? Copy.text("profile.edit.nickname") : app.displayName)
                        .font(DesignTokens.typeBody)
                        .foregroundStyle(DesignTokens.colorTextPrimary)
                        .lineLimit(1)
                    Text(app.emailLine.isEmpty ? Copy.text("profile.emailUnbound") : app.emailLine)
                        .font(DesignTokens.typeCaption)
                        .foregroundStyle(DesignTokens.colorTextSecondary)
                        .lineLimit(1)
                }
                Spacer(minLength: 0)
            }
            .padding(.horizontal, DesignTokens.space3)
            .padding(.vertical, DesignTokens.space2)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(Copy.text("nav.profile"))
        .padding(.bottom, DesignTokens.space2)
    }
}

/// 详情区：按侧栏选择切换页面。
struct DetailView: View {
    let item: SidebarItem

    var body: some View {
        switch item {
        case .home:
            HomeView()
        case .profile:
            ProfileView()
        case .settings:
            SettingsView()
        case .service(.mc):
            McView()
        case .service(.pdf):
            ComingSoonView()
        }
    }
}

/// 未上线的服务（服务清单里保留，但明确标注“即将上线”）。
struct ComingSoonView: View {
    var body: some View {
        VStack(spacing: DesignTokens.space3) {
            Image(systemName: "hammer")
                .font(DesignTokens.typeTitle)
                .foregroundStyle(DesignTokens.colorTextSecondary)
            Text(Copy.text("status.comingSoon"))
                .font(DesignTokens.typeTitle)
                .foregroundStyle(DesignTokens.colorTextPrimary)
            Text(Copy.text("service.pdf.description"))
                .font(DesignTokens.typeBody)
                .foregroundStyle(DesignTokens.colorTextSecondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(DesignTokens.colorSurfaceBase)
        .navigationTitle(Copy.text("service.pdf.title"))
    }
}
