import CryptoKit
import Foundation
import SwiftUI

/// 图片缓存：按内容 sha256 命中，避免每次渲染都重新解码（契约 §4.6）。
///
/// 只缓存内存中的 `NSImage`，不把图片字节写到磁盘。
@MainActor
final class ImageCache {
    static let shared = ImageCache()

    private var storage: [String: NSImage] = [:]
    private var order: [String] = []
    private let limit = 64

    func image(for data: Data?) -> NSImage? {
        guard let data, !data.isEmpty else { return nil }
        let key = SHA256Digest.hex(data)
        if let cached = storage[key] { return cached }
        guard let image = NSImage(data: data) else { return nil }
        storage[key] = image
        order.append(key)
        if order.count > limit {
            let evicted = order.removeFirst()
            storage.removeValue(forKey: evicted)
        }
        return image
    }
}

/// 图片内容的 sha256（缓存键 / ETag 对照）。
enum SHA256Digest {
    static func hex(_ data: Data) -> String {
        SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
    }
}
