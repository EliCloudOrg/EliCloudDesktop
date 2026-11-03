import SwiftUI

/// 头像（圆形；没有图片时用系统占位符号）。
struct AvatarView: View {
    let data: Data?
    var size: CGFloat = 32

    var body: some View {
        Group {
            if let image = ImageCache.shared.image(for: data) {
                Image(nsImage: image)
                    .resizable()
                    .scaledToFill()
            } else {
                Image(systemName: "person.crop.circle.fill")
                    .resizable()
                    .scaledToFit()
                    .foregroundStyle(DesignTokens.colorTextDisabled)
            }
        }
        .frame(width: size, height: size)
        .clipShape(Circle())
        .accessibilityHidden(true)
    }
}

/// Hero 背景：有头图用头图 + 底部压暗层，没有就用品牌渐变（两枚登记令牌叠出柔和过渡）。
struct HeroBackground: View {
    let banner: Data?

    var body: some View {
        GeometryReader { proxy in
            ZStack(alignment: .bottom) {
                if let image = ImageCache.shared.image(for: banner) {
                    Image(nsImage: image)
                        .resizable()
                        .scaledToFill()
                        .frame(width: proxy.size.width, height: proxy.size.height)
                        .clipped()
                    Rectangle()
                        .fill(DesignTokens.brandHeroScrim)
                        .frame(height: proxy.size.height * 0.45)
                } else {
                    Rectangle()
                        .fill(DesignTokens.brandHeroGradientStart)
                        .overlay(alignment: .topTrailing) {
                            Circle()
                                .fill(DesignTokens.brandHeroGradientEnd)
                                .frame(width: proxy.size.width * 0.7, height: proxy.size.width * 0.7)
                                .blur(radius: 90)
                                .offset(x: proxy.size.width * 0.12, y: -proxy.size.height * 0.35)
                        }
                }
            }
            .clipped()
        }
        .accessibilityHidden(true)
    }
}
