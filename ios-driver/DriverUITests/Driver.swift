import XCTest

enum ConfigurationError: Error {
    case missingAppName
    case missingTapLabel
}

/// Answers the system UI the cleanroom suite can't: notification permission and "Open in…?"
/// alerts, notification banners and Notification Center cells, and Settings when a test leaves
/// the app there. It knows nothing about tests. The host runner starts it, launches the app, and
/// stops it when the suite ends; the time cap is only a backstop.
final class Driver: XCTestCase {
    private let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
    private let settings = XCUIApplication(bundleIdentifier: "com.apple.Preferences")
    private var app: XCUIApplication!
    private var appName = ""

    /// Simulator only: a host file holding how many /session_stop requests the app's current session
    /// has sent since its last other request (see ios-suite for why).
    private var stopCountPath: String?
    private var stopHoldSeconds: TimeInterval = 0
    /// A host file that reads "1" while the running test is one the hold applies to.
    private var stopHoldPath: String?
    /// Whether this background is held. Checked every tick until it is, since ios-suite flags the
    /// test from the app's log, which can trail the app going to the background.
    private var holding = false
    /// This background's hold ran out with no stop. It stays out until the app is seen in the
    /// foreground, so a return that doesn't take can't start a second full hold.
    private var holdOver = false
    private var holdLogged = false

    /// Alert buttons the driver taps, in preference order. Anything else is logged, not tapped.
    private let acceptButtons = ["Allow", "Open"]

    func testDrive() throws {
        continueAfterFailure = true
        let env = ProcessInfo.processInfo.environment
        let bundleId = env["CLEANROOM_BUNDLE_ID"] ?? "io.teak.app.unity.dev"
        appName = env["CLEANROOM_APP_NAME"] ?? ""
        stopCountPath = env["CLEANROOM_STOP_COUNT_PATH"]
        stopHoldSeconds = TimeInterval(env["CLEANROOM_STOP_HOLD_SECONDS"] ?? "") ?? 0
        stopHoldPath = env["CLEANROOM_STOP_HOLD_PATH"]
        let seconds = TimeInterval(env["CLEANROOM_DRIVER_SECONDS"] ?? "") ?? 1800
        app = XCUIApplication(bundleIdentifier: bundleId)
        log("config bundle=\(bundleId) app_name=\(appName.isEmpty ? "(none)" : appName) seconds=\(Int(seconds)) " +
            "stop_hold=\(Int(stopHoldSeconds)) stop_count_path=\(stopCountPath != nil) stop_hold_path=\(stopHoldPath != nil)")
        // An empty name would match every app's notifications, so refuse to start.
        guard !appName.isEmpty else { throw ConfigurationError.missingAppName }
        log("ready")

        let deadline = Date().addingTimeInterval(seconds)
        var settingsSince: Date?
        var backgroundSince: Date?
        var lastNotificationCenterCheck = Date.distantPast
        var lastAlertLabel: String?

        while Date() < deadline {
            if let alert = visibleAlert() {
                if tapAccept(alert) {
                    lastAlertLabel = nil
                    continue
                }
                if alert.label != lastAlertLabel {
                    log("alert not handled: \"\(alert.label)\" buttons=\(alert.buttons.allElementsBoundByIndex.map(\.label))")
                    lastAlertLabel = alert.label
                }
            }

            let backgrounded = isBackgrounded(app)
            if backgrounded {
                backgroundSince = backgroundSince ?? Date()
                // ios-suite zeroes the count at the session's last send before backgrounding, so any
                // stop it shows is this background's, even one sent before this tick noticed.
                if !holding && holdApplies() {
                    holding = true
                    log("holding the background for session_stop")
                }
            } else {
                resetBackground(&backgroundSince)
            }
            let mayReturn = !backgrounded || stopSentOrHoldOver(since: backgroundSince!)

            if mayReturn && tapBanner() {
                backgroundSince = nil
                continue
            }

            if settings.state == .runningForeground {
                settingsSince = settingsSince ?? Date()
                if Date().timeIntervalSince(settingsSince!) > 3 && mayReturn {
                    log("Settings in foreground, returning to app")
                    app.activate()
                    settingsSince = nil
                    // The app takes a moment to come up. Restart the background clock so the next
                    // tick doesn't read a long background and pull Notification Center over it.
                    backgroundSince = nil
                }
            } else {
                settingsSince = nil
            }

            if backgrounded && mayReturn {
                // Banners go to Notification Center after a few seconds, so look there too, but not
                // every tick: each pull-down is slow and noisy.
                if let since = backgroundSince, Date().timeIntervalSince(since) > 8,
                    Date().timeIntervalSince(lastNotificationCenterCheck) > 10 {
                    lastNotificationCenterCheck = Date()
                    if tapNotificationCenterCell() {
                        backgroundSince = nil
                        continue
                    }
                }
            }

            Thread.sleep(forTimeInterval: 1)
        }
        log("time cap reached")
    }

    // MARK: - Safari link tap

    /// Taps the link named CLEANROOM_TAP_LABEL on the page Safari already has open, as a player
    /// would, then answers an "Open in…?" alert if one shows. script/safari-links opens the page
    /// and reads the app's log; this only does the tap.
    func testTapLink() throws {
        let label = ProcessInfo.processInfo.environment["CLEANROOM_TAP_LABEL"] ?? ""
        guard !label.isEmpty else { throw ConfigurationError.missingTapLabel }
        let safari = XCUIApplication(bundleIdentifier: "com.apple.mobilesafari")
        safari.activate()

        let link = safari.links[label]
        guard link.waitForExistence(timeout: 30) else {
            log("tap: no link \"\(label)\" in Safari")
            XCTFail("no link \"\(label)\" in Safari")
            return
        }
        log("tap: tapping \"\(label)\"")
        link.tap()

        // A universal link opens the app with no prompt, but answer one if it shows.
        let bundleId = ProcessInfo.processInfo.environment["CLEANROOM_BUNDLE_ID"] ?? "io.teak.app.unity.dev"
        let target = XCUIApplication(bundleIdentifier: bundleId)
        let until = Date().addingTimeInterval(10)
        while target.state != .runningForeground && Date() < until {
            if let alert = visibleAlert(), tapAccept(alert) { break }
            Thread.sleep(forTimeInterval: 0.5)
        }

        // Say where the tap ended up, so a link that opened in Safari instead of the app is visible.
        let opened = target.state == .runningForeground
        log("tap: app in foreground \(opened), safari state \(safari.state.rawValue)")
        if !opened {
            let labels = (safari.buttons.allElementsBoundByIndex + safari.links.allElementsBoundByIndex).prefix(25).map(\.label)
            log("tap: Safari shows \(labels)")
        }
    }

    // MARK: - Session stop hold

    /// The app is in the foreground: the next background starts a fresh hold.
    private func resetBackground(_ backgroundSince: inout Date?) {
        backgroundSince = nil
        holding = false
        holdOver = false
        holdLogged = false
    }

    private func holdApplies() -> Bool {
        guard let path = stopHoldPath,
              let text = try? String(contentsOfFile: path, encoding: .utf8) else { return false }
        return text.trimmingCharacters(in: .whitespacesAndNewlines) == "1"
    }

    private func readStopCount() -> Int? {
        guard let path = stopCountPath,
              let text = try? String(contentsOfFile: path, encoding: .utf8) else { return nil }
        return Int(text.trimmingCharacters(in: .whitespacesAndNewlines))
    }

    /// Whether the app may be brought back: its /session_stop for this background has gone out, or
    /// the hold ran out (a stop that never comes then fails the test instead of stalling the run).
    private func stopSentOrHoldOver(since backgroundSince: Date) -> Bool {
        guard stopHoldSeconds > 0, holding, !holdOver else { return true }
        let waited = Date().timeIntervalSince(backgroundSince)
        if let count = readStopCount(), count > 0 {
            if !holdLogged { log(String(format: "session_stop sent after %.1fs in background", waited)) }
            holdLogged = true
            return true
        }
        if waited > stopHoldSeconds {
            holdOver = true
            if !holdLogged { log("no session_stop after \(Int(stopHoldSeconds))s in background, returning anyway") }
            holdLogged = true
            return true
        }
        return false
    }

    // MARK: - Alerts

    private func visibleAlert() -> XCUIElement? {
        let alert = springboard.alerts.firstMatch
        return alert.exists ? alert : nil
    }

    private func tapAccept(_ alert: XCUIElement) -> Bool {
        for label in acceptButtons {
            let button = alert.buttons[label]
            if button.exists {
                log("tap alert \"\(alert.label)\" -> \(label)")
                return tapCenter(of: button)
            }
        }
        return false
    }

    // MARK: - Notifications

    /// A notification from our app, wherever SpringBoard is showing it. Banners, Notification
    /// Center cells, and lock screen cells all carry the app's display name in their label.
    private func notificationElement() -> XCUIElement? {
        let predicate = NSPredicate(format: "identifier == 'NotificationShortLookView' AND label BEGINSWITH[c] %@", appName)
        let element = springboard.descendants(matching: .any).matching(predicate).firstMatch
        return element.exists ? element : nil
    }

    /// The pull-down shows the Cover Sheet (the lock screen's notification list). Our cell sits in it
    /// with a hittable frame, but a tap there only slides the cell to show its swipe actions, so a
    /// cell on the Cover Sheet isn't a banner.
    private func coverSheetShowing() -> Bool {
        springboard.windows["SBCoverSheetWindow"].exists
    }

    private func tapBanner() -> Bool {
        guard !coverSheetShowing(), let banner = notificationElement() else { return false }
        log("tap notification \"\(banner.label)\"")
        return tapCenter(of: banner)
    }

    /// Opens our notification from the Cover Sheet: scroll it out from under the "N Notifications"
    /// pill, swipe it right, and tap Open. Succeeds only once the app is in the foreground.
    private func tapNotificationCenterCell() -> Bool {
        log("app backgrounded with no banner, checking Notification Center")
        let top = springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.2, dy: 0.0))
        let down = springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.2, dy: 0.7))
        top.press(forDuration: 0.1, thenDragTo: down)
        Thread.sleep(forTimeInterval: 1.5)

        var cell = notificationElement()
        var scrolls = 0
        while let hidden = cell, !hidden.isHittable, scrolls < 2 {
            scrolls += 1
            // Start mid-screen: a drag up from near the bottom edge closes the Cover Sheet instead.
            let from = springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.6))
            let to = springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.35))
            from.press(forDuration: 0.1, thenDragTo: to)
            Thread.sleep(forTimeInterval: 1)
            cell = notificationElement()
        }
        guard let cell, cell.isHittable else {
            log("no notification from \(appName) on screen in Notification Center, frame \(cell?.frame ?? .zero)")
            XCUIDevice.shared.press(.home)
            return false
        }

        log("open notification \"\(cell.label)\" from Notification Center")
        let open = springboard.buttons.matching(identifier: "swipe-action-button-identifier")["Open"]
        if !open.exists {
            let frame = cell.frame
            let origin = springboard.coordinate(withNormalizedOffset: .zero)
            origin.withOffset(CGVector(dx: frame.minX + 30, dy: frame.midY))
                .press(forDuration: 0.1, thenDragTo: origin.withOffset(CGVector(dx: frame.minX + 200, dy: frame.midY)))
            Thread.sleep(forTimeInterval: 1)
        }
        guard open.exists, tapCenter(of: open) else {
            log("no Open action on the notification")
            XCUIDevice.shared.press(.home)
            return false
        }
        if app.wait(for: .runningForeground, timeout: 5) { return true }
        log("Open tapped but the app didn't come to the foreground")
        XCUIDevice.shared.press(.home)
        return false
    }

    /// Banners and alerts can vanish between the query and the tap. An element tap would then fail
    /// the test and end the driver, so tap the point it was at instead, and give SpringBoard a
    /// moment to animate it away before the next look.
    private func tapCenter(of element: XCUIElement) -> Bool {
        let frame = element.frame
        guard !frame.isEmpty else { return false }
        let origin = springboard.coordinate(withNormalizedOffset: .zero)
        origin.withOffset(CGVector(dx: frame.midX, dy: frame.midY)).tap()
        Thread.sleep(forTimeInterval: 2)
        return true
    }

    private func isBackgrounded(_ app: XCUIApplication) -> Bool {
        switch app.state {
        case .runningBackground, .runningBackgroundSuspended:
            return true
        default:
            return false
        }
    }

    private func log(_ message: String) {
        print("[driver] \(message)")
    }
}
