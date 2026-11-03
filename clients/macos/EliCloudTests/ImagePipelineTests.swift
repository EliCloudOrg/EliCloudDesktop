import XCTest
@testable import EliCloud

/// 图片管线：解码 → 裁切 → 缩放 → PNG（Stage 4 §4.6）。
final class ImagePipelineTests: XCTestCase {

    func testCropUsesNormalizedCoordinates() throws {
        let image = try makeImage(width: 100, height: 50)
        let rightHalf = try XCTUnwrap(
            ImagePipeline.cropped(image, normalizedRect: CGRect(x: 0.5, y: 0, width: 0.5, height: 1))
        )
        XCTAssertEqual(rightHalf.width, 50)
        XCTAssertEqual(rightHalf.height, 50)
    }

    func testDownscaleKeepsAspectRatioAndDoesNotUpscale() throws {
        let image = try makeImage(width: 100, height: 50)
        let small = try XCTUnwrap(ImagePipeline.downscaled(image, maxDimension: 40))
        XCTAssertEqual(small.width, 40)
        XCTAssertEqual(small.height, 20)

        let unchanged = try XCTUnwrap(ImagePipeline.downscaled(small, maxDimension: 512))
        XCTAssertEqual(unchanged.width, 40, "小于上限时不应该放大")
    }

    func testPngRoundTripIsSupported() throws {
        let image = try makeImage(width: 32, height: 24)
        let data = try ImagePipeline.pngData(image)
        XCTAssertFalse(data.isEmpty)
        XCTAssertTrue(ImagePipeline.isSupportedImage(data))

        let decoded = try ImagePipeline.loadImage(from: data)
        XCTAssertEqual(decoded.width, 32)
        XCTAssertEqual(decoded.height, 24)
    }

    func testCropThenScaleToAvatarBounds() throws {
        let image = try makeImage(width: 1200, height: 800)
        let data = try ImagePipeline.pngData(
            cropped: image,
            normalizedRect: CGRect(x: 0.1, y: 0.1, width: 0.5, height: 0.5),
            maxDimension: ImagePipeline.avatarMaxDimension
        )
        let decoded = try ImagePipeline.loadImage(from: data)
        XCTAssertLessThanOrEqual(max(decoded.width, decoded.height), Int(ImagePipeline.avatarMaxDimension))
        XCTAssertEqual(decoded.width, decoded.height, "1:1 选区裁出来必须是正方形")
    }

    func testUnsupportedBytesAreRejected() {
        XCTAssertFalse(ImagePipeline.isSupportedImage(Data("not an image".utf8)))
    }

    private func makeImage(width: Int, height: Int) throws -> CGImage {
        let space = try XCTUnwrap(CGColorSpace(name: CGColorSpace.sRGB))
        let context = try XCTUnwrap(
            CGContext(
                data: nil,
                width: width,
                height: height,
                bitsPerComponent: 8,
                bytesPerRow: 0,
                space: space,
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
            )
        )
        context.setFillColor(NSColor.systemBlue.cgColor)
        context.fill(CGRect(x: 0, y: 0, width: width, height: height))
        return try XCTUnwrap(context.makeImage())
    }
}

/// 图片缓存键：sha256 走 CryptoKit（不自造轮子）。
@MainActor
final class ImageCacheTests: XCTestCase {

    func testSha256KnownVector() {
        XCTAssertEqual(
            SHA256Digest.hex(Data("abc".utf8)),
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
        )
    }

    func testCacheReturnsImageForPngData() throws {
        let pipeline = try ImagePipeline.pngData(makeImage())
        XCTAssertNotNil(ImageCache.shared.image(for: pipeline))
        XCTAssertNil(ImageCache.shared.image(for: Data()))
        XCTAssertNil(ImageCache.shared.image(for: nil))
    }

    private func makeImage() throws -> CGImage {
        let space = try XCTUnwrap(CGColorSpace(name: CGColorSpace.sRGB))
        let context = try XCTUnwrap(
            CGContext(
                data: nil,
                width: 8,
                height: 8,
                bitsPerComponent: 8,
                bytesPerRow: 0,
                space: space,
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
            )
        )
        context.setFillColor(NSColor.systemGreen.cgColor)
        context.fill(CGRect(x: 0, y: 0, width: 8, height: 8))
        return try XCTUnwrap(context.makeImage())
    }
}
