import AppKit
import XCTest
@testable import EliCloud

/// 裁切模型：归一化选区必须换算成**像素**上的目标长宽比。
@MainActor
final class CropModelTests: XCTestCase {

    func testAvatarSelectionIsSquareInPixels() throws {
        let model = CropModel(source: try makeImage(width: 1200, height: 800), aspect: 1)
        let pixelWidth = model.rect.width * 1200
        let pixelHeight = model.rect.height * 800
        XCTAssertEqual(pixelWidth, pixelHeight, accuracy: 1, "1:1 选区在像素空间必须是正方形")
        // 1200×800 上最大的正方形是 800×800：占宽 2/3，水平居中、垂直占满。
        XCTAssertEqual(model.rect.width, 2.0 / 3.0, accuracy: 0.01)
        XCTAssertEqual(model.rect.minX, 1.0 / 6.0, accuracy: 0.01)
        XCTAssertEqual(model.rect.minY, 0, accuracy: 0.01)
    }

    func testBannerSelectionIsFiveToOneInPixels() throws {
        let model = CropModel(source: try makeImage(width: 1200, height: 800), aspect: 5)
        let ratio = (model.rect.width * 1200) / (model.rect.height * 800)
        XCTAssertEqual(ratio, 5, accuracy: 0.05)
        XCTAssertEqual(model.rect.width, 1, accuracy: 0.02, "5:1 在 3:2 图上占满宽度")
    }

    func testResizeKeepsPixelAspect() throws {
        let model = CropModel(source: try makeImage(width: 1200, height: 800), aspect: 1)
        model.resize(by: -0.15)
        let ratio = (model.rect.width * 1200) / (model.rect.height * 800)
        XCTAssertEqual(ratio, 1, accuracy: 0.05)
        XCTAssertGreaterThanOrEqual(model.rect.width, 0.05)
    }

    func testMoveStaysInsideImage() throws {
        let model = CropModel(source: try makeImage(width: 1200, height: 800), aspect: 1)
        model.move(by: CGSize(width: 5, height: 5))
        XCTAssertLessThanOrEqual(model.rect.maxX, 1.0001)
        XCTAssertLessThanOrEqual(model.rect.maxY, 1.0001)
        model.move(by: CGSize(width: -5, height: -5))
        XCTAssertGreaterThanOrEqual(model.rect.minX, -0.0001)
        XCTAssertGreaterThanOrEqual(model.rect.minY, -0.0001)
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
        context.setFillColor(NSColor.systemTeal.cgColor)
        context.fill(CGRect(x: 0, y: 0, width: width, height: height))
        return try XCTUnwrap(context.makeImage())
    }
}
