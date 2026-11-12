import XCTest

/// Spike probe: answers the notification prompt with Allow, taps the app's Test Purchase button
/// once, then watches for the system purchase sheet, attaches a screenshot and the accessibility
/// hierarchy, and reports whether an "Environment: Sandbox" label is visible. After the one
/// Test Purchase tap it never taps again, so confirming the purchase stays with a person.
final class SandboxSheet: XCTestCase {
    /// Test Purchase is drawn by Unity and sits below a test list that grows while the suite runs,
    /// so find it by pixels: the first tall run of light-gray pixels down the middle of the screen
    /// (the button) after the status text and list (thin light text on dark blue). Returns the
    /// vertical centre as a fraction of the screen height.
    private func findFirstButtonY() -> CGFloat? {
        guard let cg = XCUIScreen.main.screenshot().image.cgImage else { return nil }
        let w = cg.width, h = cg.height
        var pixels = [UInt8](repeating: 0, count: w * h * 4)
        guard let ctx = CGContext(data: &pixels, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4,
                                  space: CGColorSpaceCreateDeviceRGB(),
                                  bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return nil }
        ctx.draw(cg, in: CGRect(x: 0, y: 0, width: w, height: h))
        // Sample a few columns near the left edge, where the list text never reaches the button's margins.
        let xs = [Int(Double(w) * 0.08), Int(Double(w) * 0.12)]
        var runStart: Int?
        for y in 0..<h {
            let bright = xs.allSatisfy { x in
                let i = (y * w + x) * 4
                return pixels[i] > 170 && pixels[i + 1] > 170 && pixels[i + 2] > 170
            }
            if bright {
                runStart = runStart ?? y
            } else if let start = runStart {
                if y - start > Int(Double(h) * 0.03) { return CGFloat(start + y) / 2 / CGFloat(h) }
                runStart = nil
            }
        }
        return nil
    }

    private func attachScreenshot(_ name: String) {
        let shot = XCTAttachment(screenshot: XCUIScreen.main.screenshot())
        shot.name = name
        shot.lifetime = .keepAlways
        add(shot)
    }

    /// CLEANROOM_CONFIRM=1: taps Purchase on a sheet that shows the "Sandbox" title and the
    /// no-charge text, once, then only watches for an authentication prompt (never answers one).
    func testConfirmSandboxSheet() throws {
        continueAfterFailure = true
        let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
        func labels() -> [String] { springboard.staticTexts.allElementsBoundByIndex.map(\.label) }
        let before = labels()
        let isSandbox = before.contains("Sandbox") && before.contains { $0.contains("You will not be charged") }
        attachScreenshot("confirm-before")
        NSLog("SANDBOXCONFIRM sandboxSheet=%@ labels=%@", isSandbox ? "yes" : "no", before.joined(separator: " | "))
        guard isSandbox else { return XCTFail("not a sandbox sheet, not tapping") }
        let button = springboard.buttons["Purchase"]
        guard button.exists else { return XCTFail("no Purchase button") }
        NSLog("SANDBOXCONFIRM tap Purchase")
        button.tap()
        for i in 1...12 {
            Thread.sleep(forTimeInterval: 3)
            attachScreenshot("confirm-after-\(i * 3)s")
            NSLog("SANDBOXCONFIRM +%ds labels=%@ alerts=%@", i * 3, labels().joined(separator: " | "),
                  springboard.alerts.allElementsBoundByIndex.map(\.label).joined(separator: " | "))
        }
    }

    /// Run A in one pass: taps Test Purchase, then Purchase on the "Sandbox" sheet, and enters the
    /// sandbox account password (from CLEANROOM_SANDBOX_PASSWORD, never logged or attached) only
    /// when the sign-in prompt shows "[Environment: Sandbox]". Afterwards it only watches.
    func testPurchaseFlow() throws {
        continueAfterFailure = true
        let env = ProcessInfo.processInfo.environment
        let bundleId = env["CLEANROOM_BUNDLE_ID"] ?? "io.teak.app.unity.dev"
        let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
        let app = XCUIApplication(bundleIdentifier: bundleId)
        func labels() -> [String] { springboard.staticTexts.allElementsBoundByIndex.map(\.label) }
        func waitFor(_ what: String, _ seconds: TimeInterval, _ test: () -> Bool) -> Bool {
            let end = Date().addingTimeInterval(seconds)
            while Date() < end { if test() { return true }; Thread.sleep(forTimeInterval: 1) }
            NSLog("SANDBOXFLOW timeout waiting for %@; labels=%@", what, labels().joined(separator: " | "))
            return false
        }

        // Answer the notification prompt if it's up, then let the app settle.
        if app.state != .runningForeground { app.activate() }
        guard waitFor("app foreground", 30, { app.state == .runningForeground }) else {
            attachScreenshot("flow-fail-not-foreground"); return XCTFail("app not in foreground, not tapping")
        }
        let alert = springboard.alerts.firstMatch
        if alert.exists, alert.buttons["Allow"].exists { alert.buttons["Allow"].tap() }
        Thread.sleep(forTimeInterval: 3)
        attachScreenshot("flow-1-before-test-purchase")
        guard let y = findFirstButtonY() else { return XCTFail("couldn't find the Test Purchase button") }
        NSLog("SANDBOXFLOW tap Test Purchase at y=%.4f", Double(y))
        app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: y)).tap()

        guard waitFor("sandbox sheet", 60, { labels().contains("Sandbox") && labels().contains { $0.contains("You will not be charged") } }) else {
            attachScreenshot("flow-fail-no-sheet"); return XCTFail("no sandbox sheet")
        }
        attachScreenshot("flow-2-sheet")
        NSLog("SANDBOXFLOW sheet labels=%@", labels().joined(separator: " | "))
        springboard.buttons["Purchase"].tap()

        guard waitFor("sign-in prompt", 30, { labels().contains { $0.contains("Enter the password for") } }) else {
            attachScreenshot("flow-fail-no-prompt"); return XCTFail("no password prompt")
        }
        guard labels().contains(where: { $0.contains("Environment: Sandbox") }) else {
            attachScreenshot("flow-fail-no-banner"); return XCTFail("prompt has no [Environment: Sandbox] line, not typing")
        }
        attachScreenshot("flow-3-prompt-with-banner")
        NSLog("SANDBOXFLOW prompt shows [Environment: Sandbox]")
        guard let password = env["CLEANROOM_SANDBOX_PASSWORD"], !password.isEmpty else { return XCTFail("no password in env") }
        let field = springboard.secureTextFields.firstMatch
        guard field.waitForExistence(timeout: 10) else { return XCTFail("no password field") }
        field.tap()
        field.typeText(password)
        springboard.buttons["Sign In"].tap()
        NSLog("SANDBOXFLOW tapped Sign In")

        for i in 1...15 {
            Thread.sleep(forTimeInterval: 3)
            attachScreenshot("flow-4-after-signin-\(i * 3)s")
            NSLog("SANDBOXFLOW +%ds labels=%@ alerts=%@", i * 3, labels().joined(separator: " | "),
                  springboard.alerts.allElementsBoundByIndex.map(\.label).joined(separator: " | "))
        }
    }

    /// Enters the sandbox password on a sign-in prompt that is already up and shows
    /// "Environment: Sandbox", taps Sign In, then only watches.
    func testEnterPassword() throws {
        continueAfterFailure = true
        let env = ProcessInfo.processInfo.environment
        let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
        func labels() -> [String] { springboard.staticTexts.allElementsBoundByIndex.map(\.label) }
        NSLog("SANDBOXFLOW prompt labels=%@", labels().joined(separator: " | "))
        guard labels().contains(where: { $0.contains("Enter the password for") }),
              labels().contains(where: { $0.contains("Environment: Sandbox") }) else {
            attachScreenshot("pw-fail-no-prompt"); return XCTFail("no sandbox sign-in prompt, not typing")
        }
        attachScreenshot("pw-1-prompt")
        guard let password = env["CLEANROOM_SANDBOX_PASSWORD"], !password.isEmpty else { return XCTFail("no password in env") }
        let field = springboard.secureTextFields.firstMatch
        guard field.waitForExistence(timeout: 10) else { return XCTFail("no password field") }
        field.tap()
        field.typeText(password)
        springboard.buttons["Sign In"].tap()
        NSLog("SANDBOXFLOW tapped Sign In")
        for i in 1...15 {
            Thread.sleep(forTimeInterval: 3)
            attachScreenshot("pw-2-after-signin-\(i * 3)s")
            NSLog("SANDBOXFLOW +%ds labels=%@ alerts=%@", i * 3, labels().joined(separator: " | "),
                  springboard.alerts.allElementsBoundByIndex.map(\.label).joined(separator: " | "))
        }
    }

    func testCaptureSheet() throws {
        // A lost connection to SpringBoard while it re-lays out must not end the watch.
        continueAfterFailure = true
        let env = ProcessInfo.processInfo.environment
        let bundleId = env["CLEANROOM_BUNDLE_ID"] ?? "io.teak.app.unity.dev"
        let seconds = TimeInterval(env["CLEANROOM_DRIVER_SECONDS"] ?? "") ?? 120
        let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
        let app = XCUIApplication(bundleIdentifier: bundleId)

        func labels() -> [String] {
            (springboard.staticTexts.allElementsBoundByIndex + app.staticTexts.allElementsBoundByIndex).map(\.label)
        }

        // Test Purchase is drawn by Unity, so the accessibility tree can't see it. It's the first
        // button under the status text; this is its position in a 1290x2796 screenshot.
        let testPurchase = app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.1635))
        // CLEANROOM_NO_TAP=1 watches only: used when a person has already started the purchase.
        var tappedTestPurchase = env["CLEANROOM_NO_TAP"] == "1"
        var settledSince: Date?

        var found = false
        let deadline = Date().addingTimeInterval(seconds)
        var lastShot = Date.distantPast
        while Date() < deadline {
            if Date().timeIntervalSince(lastShot) > 20 {
                attachScreenshot("watch-\(Int(Date().timeIntervalSince1970))")
                lastShot = Date()
            }
            if !tappedTestPurchase {
                let alert = springboard.alerts.firstMatch
                if alert.exists, alert.buttons["Allow"].exists {
                    NSLog("SANDBOXSHEET tap Allow on \"%@\"", alert.label)
                    alert.buttons["Allow"].tap()
                    settledSince = nil
                } else if app.state == .runningForeground {
                    settledSince = settledSince ?? Date()
                    if Date().timeIntervalSince(settledSince!) > 12 {
                        attachScreenshot("before-test-purchase")
                        NSLog("SANDBOXSHEET tap Test Purchase")
                        testPurchase.tap()
                        tappedTestPurchase = true
                    }
                }
            }
            if labels().contains(where: { $0.localizedCaseInsensitiveContains("Environment: Sandbox") }) {
                found = true
                break
            }
            Thread.sleep(forTimeInterval: 1)
        }

        attachScreenshot("sheet")
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
