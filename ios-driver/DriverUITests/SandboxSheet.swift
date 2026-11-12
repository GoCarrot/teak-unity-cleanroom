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

    /// Taps OK on the "You're all set" purchase-complete alert, so the phone isn't left with it up.
    /// Returns whether it found and dismissed one.
    @discardableResult
    private func dismissPurchaseComplete(waiting seconds: TimeInterval) -> Bool {
        let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
        let end = Date().addingTimeInterval(seconds)
        repeat {
            let up = springboard.staticTexts.allElementsBoundByIndex.contains { $0.label.contains("Your purchase was successful") }
            if up {
                let ok = springboard.buttons["OK"].exists ? springboard.buttons["OK"] : springboard.alerts.buttons.firstMatch
                if ok.exists {
                    NSLog("SANDBOXFLOW tap OK on purchase-complete alert")
                    ok.tap()
                    return true
                }
            }
            Thread.sleep(forTimeInterval: 1)
        } while Date() < end
        return false
    }

    /// All labels on an app's screen, read from one snapshot so elements disappearing mid-read
    /// can't fail the test. Empty if the snapshot can't be taken.
    private func snapshotLabels(_ app: XCUIApplication) -> [String] {
        guard let root = try? app.snapshot() else { return [] }
        var out: [String] = []
        func walk(_ e: XCUIElementSnapshot) {
            if !e.label.isEmpty { out.append(e.label) }
            e.children.forEach { walk($0) }
        }
        walk(root)
        return out
    }

    /// Run B in one resumable pass: whatever state the phone is in (nothing yet, sheet up, prompt up,
    /// purchase done), do the next step once. Each tap is gated on what the screen shows: Purchase
    /// only on the "Sandbox" sheet with the no-charge text, the password only on the prompt showing
    /// "Environment: Sandbox" (password from CLEANROOM_SANDBOX_PASSWORD, never logged or attached),
    /// OK only on the purchase-complete alert.
    func testResumeFlow() throws {
        continueAfterFailure = true
        let env = ProcessInfo.processInfo.environment
        let bundleId = env["CLEANROOM_BUNDLE_ID"] ?? "io.teak.app.unity.dev"
        let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
        let app = XCUIApplication(bundleIdentifier: bundleId)
        var tappedTestPurchase = false, tappedPurchase = false, typedPassword = false
        let end = Date().addingTimeInterval(TimeInterval(env["CLEANROOM_DRIVER_SECONDS"] ?? "") ?? 240)
        var settledSince: Date?
        var lastLabels = ""
        while Date() < end {
            let labels = snapshotLabels(springboard)
            let joined = labels.joined(separator: " | ")
            if joined != lastLabels { NSLog("SANDBOXFLOW labels=%@", joined); lastLabels = joined }
            if labels.contains(where: { $0.contains("Your purchase was successful") }) {
                let ok = springboard.buttons["OK"].exists ? springboard.buttons["OK"] : springboard.alerts.buttons.firstMatch
                if ok.exists { NSLog("SANDBOXFLOW tap OK on purchase-complete alert"); ok.tap(); attachScreenshot("resume-done"); return }
            } else if labels.contains(where: { $0.contains("Enter the password for") }) {
                guard labels.contains(where: { $0.contains("Environment: Sandbox") }) else {
                    attachScreenshot("resume-fail-no-banner"); return XCTFail("sign-in prompt without Environment: Sandbox, not typing")
                }
                if !typedPassword, let password = env["CLEANROOM_SANDBOX_PASSWORD"], !password.isEmpty {
                    attachScreenshot("resume-prompt")
                    let field = springboard.secureTextFields.firstMatch
                    if field.waitForExistence(timeout: 10) {
                        field.tap(); field.typeText(password)
                        springboard.buttons["Sign In"].tap()
                        typedPassword = true
                        NSLog("SANDBOXFLOW typed password, tapped Sign In")
                    }
                }
            } else if labels.contains("Sandbox") && labels.contains(where: { $0.contains("You will not be charged") }) {
                if !tappedPurchase, springboard.buttons["Purchase"].exists {
                    attachScreenshot("resume-sheet")
                    NSLog("SANDBOXFLOW tap Purchase")
                    springboard.buttons["Purchase"].tap()
                    tappedPurchase = true
                }
            } else if !tappedTestPurchase {
                let alert = springboard.alerts.firstMatch
                if alert.exists, alert.buttons["Allow"].exists {
                    alert.buttons["Allow"].tap(); settledSince = nil
                } else {
                    if app.state != .runningForeground { app.activate() }
                    if app.state == .runningForeground {
                        settledSince = settledSince ?? Date()
                        if Date().timeIntervalSince(settledSince!) > 3, let y = findFirstButtonY() {
                            attachScreenshot("resume-before-test-purchase")
                            NSLog("SANDBOXFLOW tap Test Purchase at y=%.4f", Double(y))
                            app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: y)).tap()
                            tappedTestPurchase = true
                        }
                    }
                }
            }
            Thread.sleep(forTimeInterval: 1)
        }
        attachScreenshot("resume-timeout")
        XCTFail("flow did not reach the purchase-complete alert")
    }

    /// Dismisses a purchase-complete alert left up by an earlier run.
    func testDismissPurchaseAlert() throws {
        continueAfterFailure = true
        attachScreenshot("dismiss-before")
        XCTAssertTrue(dismissPurchaseComplete(waiting: 10), "no purchase-complete alert found")
        Thread.sleep(forTimeInterval: 2)
        attachScreenshot("dismiss-after")
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
        let dismissed = dismissPurchaseComplete(waiting: 30)
        NSLog("SANDBOXFLOW purchase-complete alert dismissed=%@", dismissed ? "yes" : "no")
        for i in 1...5 {
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
        let dismissed = dismissPurchaseComplete(waiting: 30)
        NSLog("SANDBOXFLOW purchase-complete alert dismissed=%@", dismissed ? "yes" : "no")
        for i in 1...5 {
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
