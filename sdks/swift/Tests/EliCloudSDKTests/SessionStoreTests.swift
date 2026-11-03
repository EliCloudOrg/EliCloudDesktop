import XCTest
@testable import EliCloudSDK

final class SessionStoreTests: XCTestCase {

    private func makeSession(expiresIn: TimeInterval = 3600) -> SessionState {
        SessionState(
            accessToken: "at_example",
            refreshToken: "rt_example",
            scope: ["openid", "profile", "email", "mc:whitelist"],
            subject: "u_example01",
            username: "alice",
            email: "alice@example.com",
            emailVerified: true,
            expiresAt: Date().addingTimeInterval(expiresIn)
        )
    }

    func testAdoptAndClear() async {
        let store = SessionStore()
        await store.adopt(makeSession())
        let current = await store.current
        XCTAssertEqual(current?.subject, "u_example01")
        XCTAssertEqual(await store.accessToken(), "at_example")

        await store.clear()
        let cleared = await store.current
        XCTAssertNil(cleared)
        XCTAssertNil(await store.accessToken())
    }

    func testExpiredTokenIsNotUsable() async {
        let store = SessionStore(initial: makeSession(expiresIn: -10))
        let token = await store.accessToken()
        XCTAssertNil(token, "过期后不返回 access token（本阶段不自动刷新）")
    }

    func testMergeIdentityKeepsTokens() async {
        let store = SessionStore(initial: makeSession())
        await store.mergeIdentity(
            subject: "u_example01",
            username: "alice_new",
            email: nil,
            emailVerified: false,
            scope: ["profile"]
        )
        let current = await store.current
        XCTAssertEqual(current?.accessToken, "at_example")
        XCTAssertEqual(current?.username, "alice_new")
        XCTAssertEqual(current?.email, "alice@example.com", "nil 不覆盖已有值")
        XCTAssertEqual(current?.emailVerified, false)
        XCTAssertEqual(current?.scope, ["profile"])
    }

    func testHasScopeAndExpiry() {
        let session = SessionState(accessToken: "x", scope: ["openid", "mc:whitelist"], expiresAt: Date().addingTimeInterval(60))
        XCTAssertTrue(session.hasScope("mc:whitelist"))
        XCTAssertFalse(session.hasScope("userinfo:write"))
        XCTAssertFalse(session.isExpired())
        XCTAssertTrue(session.isUsable())
        XCTAssertTrue(session.isExpired(now: Date().addingTimeInterval(120)))
    }
}
