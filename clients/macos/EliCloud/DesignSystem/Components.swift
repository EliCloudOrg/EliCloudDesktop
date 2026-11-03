import SwiftUI

/// 通用卡片容器：标题 + 内容，表面用官方材质（见 `glassBackground`）。
struct Card<Content: View>: View {
    var padding: CGFloat = DesignTokens.space4
    @ViewBuilder var content: Content

    var body: some View {
        content
            .padding(padding)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(DesignTokens.colorSurfaceRaised, in: RoundedRectangle(cornerRadius: DesignTokens.radiusCard, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: DesignTokens.radiusCard, style: .continuous)
                    .strokeBorder(DesignTokens.colorStrokeDefault, lineWidth: 1)
            )
    }
}

/// 区块标题（标题 + 说明）。
struct SectionHeader: View {
    let title: String
    var subtitle: String?

    var body: some View {
        VStack(alignment: .leading, spacing: DesignTokens.space1) {
            Text(title)
                .font(DesignTokens.typeTitle)
                .foregroundStyle(DesignTokens.colorTextPrimary)
            if let subtitle, !subtitle.isEmpty {
                Text(subtitle)
                    .font(DesignTokens.typeBody)
                    .foregroundStyle(DesignTokens.colorTextSecondary)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

/// 状态徽标：圆点 + 文案（不依赖颜色单独传达信息，文案同时出现）。
struct StatusBadge: View {
    enum Level {
        case success
        case warning
        case danger
        case neutral

        var style: AnyShapeStyle {
            switch self {
            case .success: return DesignTokens.colorStatusSuccess
            case .warning: return DesignTokens.colorStatusWarning
            case .danger: return DesignTokens.colorStatusDanger
            case .neutral: return DesignTokens.colorStatusNeutral
            }
        }
    }

    let text: String
    let level: Level

    var body: some View {
        HStack(spacing: DesignTokens.space1) {
            Circle()
                .fill(level.style)
                .frame(width: 7, height: 7)
            Text(text)
                .font(DesignTokens.typeCaption)
                .foregroundStyle(DesignTokens.colorTextSecondary)
        }
        .accessibilityElement(children: .combine)
    }
}

/// 表单里的小号说明文字。
struct FieldHint: View {
    let text: String

    var body: some View {
        Text(text)
            .font(DesignTokens.typeCaption)
            .foregroundStyle(DesignTokens.colorTextSecondary)
            .fixedSize(horizontal: false, vertical: true)
    }
}

/// 行内错误横幅（不显示错误码）。
struct InlineError: View {
    let message: String

    var body: some View {
        Label {
            Text(message)
                .font(DesignTokens.typeCaption)
                .foregroundStyle(DesignTokens.colorTextPrimary)
        } icon: {
            Image(systemName: "exclamationmark.triangle.fill")
                .foregroundStyle(DesignTokens.colorStatusWarning)
        }
        .padding(DesignTokens.space2)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Material.thin, in: RoundedRectangle(cornerRadius: DesignTokens.radiusControl, style: .continuous))
        .accessibilityElement(children: .combine)
    }
}
