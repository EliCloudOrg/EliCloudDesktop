import SwiftUI
import UniformTypeIdentifiers

/// 裁切状态：归一化选区（0…1，原点左上）+ 固定长宽比。
@MainActor
final class CropModel: ObservableObject {

    let source: CGImage
    let aspect: CGFloat
    @Published var rect: CGRect

    init(source: CGImage, aspect: CGFloat) {
        self.source = source
        self.aspect = max(0.1, aspect)
        self.rect = Self.initialRect(aspect: aspect, imageSize: CGSize(width: source.width, height: source.height))
    }

    /// 初始选区：按长宽比在图片里取最大居中区域。
    private static func initialRect(aspect: CGFloat, imageSize: CGSize) -> CGRect {
        let imageAspect = imageSize.width / max(1, imageSize.height)
        if imageAspect > aspect {
            let width = aspect / imageAspect
            return CGRect(x: (1 - width) / 2, y: 0, width: width, height: 1)
        }
        let height = imageAspect / aspect
        return CGRect(x: 0, y: (1 - height) / 2, width: 1, height: height)
    }

    func move(by delta: CGSize) {
        var moved = rect.offsetBy(dx: delta.width, dy: delta.height)
        moved.origin.x = min(max(0, moved.origin.x), 1 - moved.width)
        moved.origin.y = min(max(0, moved.origin.y), 1 - moved.height)
        rect = moved
    }

    /// 拖手柄缩放：以选区中心为锚点，保持比例，限制在图片内，最小 5%。
    func resize(by deltaWidth: CGFloat) {
        let minWidth: CGFloat = 0.05
        let center = CGPoint(x: rect.midX, y: rect.midY)
        var width = rect.width + deltaWidth
        width = min(max(minWidth, width), 1)
        var height = width / aspect
        if height > 1 {
            height = 1
            width = height * aspect
        }
        let originX = min(max(0, center.x - width / 2), 1 - width)
        let originY = min(max(0, center.y - height / 2), 1 - height)
        rect = CGRect(x: originX, y: originY, width: width, height: height)
    }

    enum Corner {
        case topLeft
        case topRight
        case bottomLeft
        case bottomRight
    }
}

/// 裁切界面：图片 + 选区 + 四角手柄；键盘/无障碍给出选区比例。
struct CropView: View {
    @ObservedObject var model: CropModel
    let title: String
    let hint: String
    let onCancel: () -> Void
    let onApply: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: DesignTokens.space3) {
            Text(title)
                .font(DesignTokens.typeTitle)
            Text(hint)
                .font(DesignTokens.typeCaption)
                .foregroundStyle(DesignTokens.colorTextSecondary)
                .fixedSize(horizontal: false, vertical: true)

            GeometryReader { proxy in
                let imageRect = fittedRect(
                    imageSize: CGSize(width: model.source.width, height: model.source.height),
                    in: proxy.size
                )
                ZStack(alignment: .topLeading) {
                    Image(nsImage: NSImage(cgImage: model.source, size: imageRect.size))
                        .resizable()
                        .interpolation(.high)
                        .frame(width: imageRect.width, height: imageRect.height)
                        .position(x: imageRect.midX, y: imageRect.midY)

                    selectionOverlay(imageRect: imageRect)
                }
            }
            .frame(minHeight: 280)
            .background(DesignTokens.colorSurfaceBase)
            .clipShape(RoundedRectangle(cornerRadius: DesignTokens.radiusCard, style: .continuous))

            HStack {
                Spacer()
                Button(Copy.text("common.cancel"), action: onCancel)
                    .keyboardShortcut(.cancelAction)
                Button(Copy.text("profile.edit.applyCrop"), action: onApply)
                    .buttonStyle(.borderedProminent)
                    .keyboardShortcut(.defaultAction)
            }
        }
        .padding(DesignTokens.space6)
        .frame(minWidth: 560, minHeight: 460)
    }

    private func selectionOverlay(imageRect: CGRect) -> some View {
        let selection = CGRect(
            x: imageRect.minX + model.rect.minX * imageRect.width,
            y: imageRect.minY + model.rect.minY * imageRect.height,
            width: model.rect.width * imageRect.width,
            height: model.rect.height * imageRect.height
        )
        return ZStack(alignment: .topLeading) {
            Path { path in
                path.addRect(imageRect)
                path.addRect(selection)
            }
            .fill(DesignTokens.brandHeroScrim, style: FillStyle(eoFill: true))

            RoundedRectangle(cornerRadius: DesignTokens.radiusControl, style: .continuous)
                .strokeBorder(DesignTokens.colorAccentDefault, lineWidth: 2)
                .frame(width: selection.width, height: selection.height)
                .position(x: selection.midX, y: selection.midY)
                .gesture(
                    DragGesture()
                        .onChanged { value in
                            model.move(by: CGSize(
                                width: value.translation.width / imageRect.width,
                                height: value.translation.height / imageRect.height
                            ))
                        }
                )
                .accessibilityLabel(Copy.text("profile.edit.cropAvatar"))
                .accessibilityValue("\(Int(model.rect.width * 100))%")

            handles(selection: selection, imageRect: imageRect)
        }
    }

    private func handles(selection: CGRect, imageRect: CGRect) -> some View {
        ForEach(Array(corners(selection).enumerated()), id: \.offset) { _, entry in
            Circle()
                .fill(DesignTokens.colorAccentDefault)
                .frame(width: 12, height: 12)
                .position(entry.point)
                .gesture(
                    DragGesture()
                        .onChanged { value in
                            model.resize(by: value.translation.width / imageRect.width)
                        }
                )
                .accessibilityHidden(true)
        }
    }

    private func corners(_ selection: CGRect) -> [(corner: CropModel.Corner, point: CGPoint)] {
        [
            (.topLeft, CGPoint(x: selection.minX, y: selection.minY)),
            (.topRight, CGPoint(x: selection.maxX, y: selection.minY)),
            (.bottomLeft, CGPoint(x: selection.minX, y: selection.maxY)),
            (.bottomRight, CGPoint(x: selection.maxX, y: selection.maxY)),
        ]
    }

    /// 图片在容器里 aspect-fit 后的矩形。
    private func fittedRect(imageSize: CGSize, in container: CGSize) -> CGRect {
        guard imageSize.width > 0, imageSize.height > 0, container.width > 0, container.height > 0 else {
            return .zero
        }
        let scale = min(container.width / imageSize.width, container.height / imageSize.height)
        let size = CGSize(width: imageSize.width * scale, height: imageSize.height * scale)
        return CGRect(
            x: (container.width - size.width) / 2,
            y: (container.height - size.height) / 2,
            width: size.width,
            height: size.height
        )
    }
}
