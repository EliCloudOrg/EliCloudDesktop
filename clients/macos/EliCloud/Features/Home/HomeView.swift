import EliCloudSDK
import SwiftUI

/// 主页：Hero（头图或品牌渐变）+ 按状态分组的服务卡片。
struct HomeView: View {
    @EnvironmentObject private var app: AppState

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: DesignTokens.space6) {
                hero

                if app.isProbing, app.mcHealth == nil {
                    HStack(spacing: DesignTokens.space2) {
                        ProgressView()
                            .controlSize(.small)
                        Text(Copy.text("home.loading"))
                            .font(DesignTokens.typeCaption)
                            .foregroundStyle(DesignTokens.colorTextSecondary)
                    }
                }

                ForEach(Array(groups.enumerated()), id: \.offset) { _, group in
                    serviceSection(group)
                }

                if groups.isEmpty, !app.isProbing {
                    Text(Copy.text("home.empty"))
                        .font(DesignTokens.typeBody)
                        .foregroundStyle(DesignTokens.colorTextSecondary)
                }
            }
            .padding(DesignTokens.space6)
            .frame(maxWidth: 980, alignment: .leading)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .background(DesignTokens.colorSurfaceBase)
        .navigationTitle(Copy.text("nav.home"))
        .toolbar {
            ToolbarItem(placement: .principal) {
                VStack(spacing: 0) {
                    Text(Copy.text("home.hero.title"))
                        .font(DesignTokens.typeBody)
                    Text(healthSummary)
                        .font(DesignTokens.typeCaption)
                        .foregroundStyle(DesignTokens.colorTextSecondary)
                }
                .accessibilityElement(children: .combine)
            }
        }
    }

    // MARK: 分组

    private struct Group {
        let titleKey: String
        let subtitleKey: String
        let availability: HomeServiceAvailability
    }

    /// 只渲染有内容的组（空组不出现）。
    private var groups: [(group: Group, services: [ServiceID])] {
        let all = ServiceID.allCases
        let definitions: [Group] = [
            Group(titleKey: "home.section.new.title", subtitleKey: "home.section.new.description", availability: .comingSoon),
            Group(titleKey: "home.section.working.title", subtitleKey: "home.section.working.description", availability: .working),
            Group(titleKey: "home.section.stopped.title", subtitleKey: "home.section.stopped.description", availability: .stopped),
        ]
        return definitions.compactMap { group in
            let services = all.filter { availability(for: $0) == group.availability }
            return services.isEmpty ? nil : (group, services)
        }
    }

    private func serviceSection(_ entry: (group: Group, services: [ServiceID])) -> some View {
        VStack(alignment: .leading, spacing: DesignTokens.space3) {
            SectionHeader(
                title: Copy.text(entry.group.titleKey),
                subtitle: Copy.text(entry.group.subtitleKey)
            )
            VStack(spacing: DesignTokens.space2) {
                ForEach(entry.services) { service in
                    ServiceCard(service: service, availability: availability(for: service)) {
                        app.selection = .service(service)
                    }
                }
            }
        }
    }

    private func availability(for service: ServiceID) -> HomeServiceAvailability {
        switch service {
        case .pdf:
            return .comingSoon
        case .mc:
            guard let health = app.mcHealth else { return .stopped }
            return health.status == .ok ? .working : .stopped
        }
    }

    private var healthSummary: String {
        guard let health = app.mcHealth else { return Copy.text("status.probing") }
        return health.status == .ok ? Copy.text("status.running") : Copy.text("status.degraded")
    }

    // MARK: Hero

    private var hero: some View {
        ZStack(alignment: .bottomLeading) {
            HeroBackground(banner: app.banner)
                .frame(height: 220)

            VStack(alignment: .leading, spacing: DesignTokens.space2) {
                Text(Copy.text("home.hero.title"))
                    .font(.largeTitle)
                    .foregroundStyle(DesignTokens.colorTextPrimary)
                Text(Copy.text("home.hero.description"))
                    .font(DesignTokens.typeBody)
                    .foregroundStyle(DesignTokens.colorTextPrimary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .padding(DesignTokens.space6)
        }
        .frame(height: 220)
        .clipShape(RoundedRectangle(cornerRadius: DesignTokens.radiusCard, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: DesignTokens.radiusCard, style: .continuous)
                .strokeBorder(DesignTokens.colorStrokeDefault, lineWidth: 1)
        )
        .accessibilityElement(children: .combine)
    }
}

/// 服务卡片：图标 / 标题 / 说明 / 状态 / 箭头。
struct ServiceCard: View {
    let service: ServiceID
    let availability: HomeServiceAvailability
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: DesignTokens.space4) {
                Image(systemName: symbol)
                    .font(DesignTokens.typeTitle)
                    .frame(width: 32, height: 32)
                    .foregroundStyle(DesignTokens.colorAccentDefault)

                VStack(alignment: .leading, spacing: DesignTokens.space1) {
                    Text(Copy.text(service.titleKey))
                        .font(DesignTokens.typeBody)
                        .foregroundStyle(DesignTokens.colorTextPrimary)
                    Text(Copy.text(service.detailKey))
                        .font(DesignTokens.typeCaption)
                        .foregroundStyle(DesignTokens.colorTextSecondary)
                }

                Spacer(minLength: DesignTokens.space2)

                StatusBadge(text: statusText, level: statusLevel)

                Image(systemName: "chevron.right")
                    .font(DesignTokens.typeCaption)
                    .foregroundStyle(DesignTokens.colorTextDisabled)
            }
            .padding(DesignTokens.space4)
            .frame(maxWidth: .infinity, alignment: .leading)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .background(
            DesignTokens.colorSurfaceRaised,
            in: RoundedRectangle(cornerRadius: DesignTokens.radiusCard, style: .continuous)
        )
        .overlay(
            RoundedRectangle(cornerRadius: DesignTokens.radiusCard, style: .continuous)
                .strokeBorder(DesignTokens.colorStrokeDefault, lineWidth: 1)
        )
        .accessibilityLabel("\(Copy.text(service.titleKey))，\(statusText)")
    }

    private var symbol: String {
        switch service {
        case .mc: return "cube.box"
        case .pdf: return "doc.richtext"
        }
    }

    private var statusText: String {
        switch availability {
        case .comingSoon: return Copy.text("status.comingSoon")
        case .working: return Copy.text("status.available")
        case .stopped: return Copy.text("status.unavailable")
        }
    }

    private var statusLevel: StatusBadge.Level {
        switch availability {
        case .comingSoon: return .neutral
        case .working: return .success
        case .stopped: return .warning
        }
    }
}

/// 卡片上展示的服务可用性（UI 专用，不代表契约）。
enum HomeServiceAvailability {
    case comingSoon
    case working
    case stopped
}
