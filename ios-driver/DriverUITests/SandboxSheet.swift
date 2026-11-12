import XCTest

/// Spike probe: finds the system purchase sheet, attaches a screenshot and the accessibility
/// hierarchy, and reports whether an "Environment: Sandbox" label is visible. It never taps.
final class SandboxSheet: XCTestCase {
    func testCaptureSheet() throws {
        let env = ProcessInfo.processInfo.environment
        let bundleId = env["CLEANROOM_BUNDLE_ID"] ?? "io.teak.app.unity.dev"
        let seconds = TimeInterval(env["CLEANROOM_DRIVER_SECONDS"] ?? "") ?? 120
        let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
        let app = XCUIApplication(bundleIdentifier: bundleId)

        func labels() -> [String] {
            (springboard.staticTexts.allElementsBoundByIndex + app.staticTexts.allElementsBoundByIndex).map(\.label)
        }

        var found = false
        let deadline = Date().addingTimeInterval(seconds)
        while Date() < deadline {
            if labels().contains(where: { $0.localizedCaseInsensitiveContains("Environment: Sandbox") }) {
                found = true
                break
            }
            Thread.sleep(forTimeInterval: 1)
        }

        let shot = XCTAttachment(screenshot: XCUIScreen.main.screenshot())
        shot.name = "sheet"
        shot.lifetime = .keepAlways
        add(shot)
        for (name, text) in [("springboard", springboard.debugDescription), ("app", app.debugDescription)] {
            let a = XCTAttachment(string: text)
            a.name = "hierarchy-\(name)"
            a.lifetime = .keepAlways
            add(a)
        }
        NSLog("SANDBOXSHEET found=%@ labels=%@", found ? "yes" : "no", labels().joined(separator: " | "))
        XCTAssertTrue(found, "no Environment: Sandbox label found")
    }
}
