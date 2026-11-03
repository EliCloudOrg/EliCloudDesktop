import AppKit
import SwiftUI

/// 半透明 chrome / 浮动卡片背景。
///
/// macOS 26 的 Liquid Glass（`.glassEffect` / `NSGlassEffectView`）需要 Xcode 26 SDK；
/// 本仓库 CI pin 在 Xcode 16.4，所以这里用官方 `NSVisualEffectView` 的 vibrancy，
/// 并在“降低透明度”开启时退回实底——不伪造玻璃，也不牺牲可读性。
struct GlassBackground: NSViewRepresentable {

    enum Style {
        case window
        case sidebar
        case card

        var material: NSVisualEffectView.Material {
            switch self {
            case .window: return .underWindowBackground
            case .sidebar: return .sidebar
            case .card: return .popover
            }
        }

        var blending: NSVisualEffectView.BlendingMode {
            switch self {
            case .window: return .behindWindow
            case .sidebar: return .behindWindow
            case .card: return .withinWindow
            }
        }
    }

    var style: Style = .card

    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        apply(to: view)
        return view
    }

    func updateNSView(_ view: NSVisualEffectView, context: Context) {
        apply(to: view)
    }

    private func apply(to view: NSVisualEffectView) {
        let reduceTransparency = NSWorkspace.shared.accessibilityDisplayShouldReduceTransparency
        view.material = reduceTransparency ? .windowBackground : style.material
        view.blendingMode = style.blending
        view.state = .active
        view.isEmphasized = false
    }
}

extension View {

    /// 给卡片 / 侧栏 / 工具栏补一层官方材质（降低透明度时自动变实底）。
    func glassBackground(_ style: GlassBackground.Style = .card, cornerRadius: CGFloat = DesignTokens.radiusCard) -> some View {
        background(
            GlassBackground(style: style)
                .clipShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
        )
    }
}
