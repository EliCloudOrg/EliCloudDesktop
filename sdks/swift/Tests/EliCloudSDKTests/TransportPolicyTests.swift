import XCTest
@testable import EliCloudSDK

final class TransportPolicyTests: XCTestCase {

    func testAnonymousOperationsMatchContract() {
        // 契约里 security: [] 的端点 —— 不该带 Bearer。
        for operation in [
            "login", "refreshToken", "registerStart", "getRegistrationStatus",
            "resendRegistrationEmail", "cancelRegistration", "mcHealth",
            "getOidcDiscovery", "getJwks", "listCoreServices",
        ] {
            XCTAssertFalse(EliCloudAuthorizationMiddleware.requiresAuthorization(operationID: operation), operation)
        }
        // 其余端点一律需要令牌。
        for operation in [
            "getUserInfo", "getMyProfile", "updateMyProfile", "uploadMyProfileImage",
            "deleteMyProfileImage", "getProfileImage", "listMyMcNames", "addMcName",
            "removeMcName", "mcIdentity",
        ] {
            XCTAssertTrue(EliCloudAuthorizationMiddleware.requiresAuthorization(operationID: operation), operation)
        }
    }

    func testHealth503PassesThrough() {
        XCTAssertTrue(EliCloudErrorMiddleware.passesThroughErrorStatus(operationID: "mcHealth", status: 503))
        XCTAssertFalse(EliCloudErrorMiddleware.passesThroughErrorStatus(operationID: "mcHealth", status: 500))
        XCTAssertFalse(EliCloudErrorMiddleware.passesThroughErrorStatus(operationID: "getMyProfile", status: 503))
    }

    func testTimeoutsFollowContract() {
        let timeouts = RequestTimeouts.contract
        XCTAssertEqual(timeouts.standard, 20)
        XCTAssertEqual(timeouts.health, 8)
        XCTAssertEqual(timeouts.profileSave, 30)
        XCTAssertEqual(timeouts.imageUpload, 60)

        XCTAssertEqual(timeouts.timeout(for: "mcHealth"), 8)
        XCTAssertEqual(timeouts.timeout(for: "updateMyProfile"), 30)
        XCTAssertEqual(timeouts.timeout(for: "uploadMyProfileImage"), 60)
        XCTAssertEqual(timeouts.timeout(for: "login"), 20)
        XCTAssertEqual(timeouts.timeout(for: "unknownFutureOperation"), 20)
    }
}
