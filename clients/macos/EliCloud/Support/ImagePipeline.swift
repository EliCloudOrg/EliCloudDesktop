import AppKit
import CoreGraphics
import CoreImage
import Foundation
import ImageIO
import UniformTypeIdentifiers

/// 图片管线（Stage 4 §4.6）：解码 → 裁切 → 缩放 → PNG。
///
/// - 解码走 CoreImage，顺带应用 EXIF 方向（`applyOrientationProperty`）；
/// - 裁切用 `CGImage.cropping(to:)`（像素坐标，原点左上）；
/// - 缩放用 Core Graphics，高质量插值，统一 sRGB；
/// - 编码 PNG 用 `NSBitmapImageRep`。
///
/// 不把图片字节写入磁盘、不写日志。
enum ImagePipeline {

    enum Failure: Error, Equatable {
        case decodeFailed
        case cropFailed
        case encodeFailed
    }

    /// 头像缩小上限（像素）。
    static let avatarMaxDimension: CGFloat = 512
    /// 头图缩小上限（像素）。
    static let bannerMaxDimension: CGFloat = 1600

    /// 解码并应用 EXIF 方向。
    static func loadImage(from data: Data) throws -> CGImage {
        guard let ciImage = CIImage(data: data, options: [.applyOrientationProperty: true]) else {
            throw Failure.decodeFailed
        }
        let context = CIContext(options: [.useSoftwareRenderer: false])
        guard let cgImage = context.createCGImage(ciImage, from: ciImage.extent) else {
            throw Failure.decodeFailed
        }
        return cgImage
    }

    /// 只接受 PNG / JPEG / WebP（按真实文件头判断，不看扩展名）。
    static func isSupportedImage(_ data: Data) -> Bool {
        guard let source = CGImageSourceCreateWithData(data as CFData, nil),
            let type = CGImageSourceGetType(source)
        else {
            return false
        }
        let identifier = type as String
        return [
            UTType.png.identifier,
            UTType.jpeg.identifier,
            UTType.webP.identifier,
        ].contains(identifier)
    }

    /// 归一化矩形（0…1，原点左上）→ 裁剪。
    static func cropped(_ image: CGImage, normalizedRect rect: CGRect) -> CGImage? {
        let width = CGFloat(image.width)
        let height = CGFloat(image.height)
        let clamped = rect.intersection(CGRect(x: 0, y: 0, width: 1, height: 1))
        guard !clamped.isEmpty else { return nil }
        let pixelRect = CGRect(
            x: (clamped.minX * width).rounded(.down),
            y: (clamped.minY * height).rounded(.down),
            width: (clamped.width * width).rounded(.down),
            height: (clamped.height * height).rounded(.down)
        )
        guard pixelRect.width >= 1, pixelRect.height >= 1 else { return nil }
        return image.cropping(to: pixelRect)
    }

    /// 等比缩到 `maxDimension` 以内（本就更小的不放大）。
    static func downscaled(_ image: CGImage, maxDimension: CGFloat) -> CGImage? {
        let width = CGFloat(image.width)
        let height = CGFloat(image.height)
        let longest = max(width, height)
        guard longest > maxDimension, width > 0, height > 0 else { return image }
        let scale = maxDimension / longest
        let targetWidth = max(1, Int((width * scale).rounded()))
        let targetHeight = max(1, Int((height * scale).rounded()))

        guard let colorSpace = CGColorSpace(name: CGColorSpace.sRGB),
            let context = CGContext(
                data: nil,
                width: targetWidth,
                height: targetHeight,
                bitsPerComponent: 8,
                bytesPerRow: 0,
                space: colorSpace,
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
            )
        else {
            return nil
        }
        context.interpolationQuality = .high
        context.draw(image, in: CGRect(x: 0, y: 0, width: targetWidth, height: targetHeight))
        return context.makeImage()
    }

    static func pngData(_ image: CGImage) throws -> Data {
        let representation = NSBitmapImageRep(cgImage: image)
        guard let data = representation.representation(using: .png, properties: [:]) else {
            throw Failure.encodeFailed
        }
        return data
    }

    /// 一步到位：裁切 → 缩放 → PNG（上传前用）。
    static func pngData(cropped image: CGImage, normalizedRect: CGRect, maxDimension: CGFloat) throws -> Data {
        guard let croppedImage = cropped(image, normalizedRect: normalizedRect) else {
            throw Failure.cropFailed
        }
        guard let scaledImage = downscaled(croppedImage, maxDimension: maxDimension) else {
            throw Failure.cropFailed
        }
        return try pngData(scaledImage)
    }
}
