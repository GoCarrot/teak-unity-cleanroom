import XCTest

/// Answers the system UI the cleanroom suite can't: notification permission and "Open in…?"
/// alerts, notification banners and Notification Center cells, and Settings when a test leaves
/// the app there. It knows nothing about tests. The host runner starts it, launches the app, and
/// stops it when the suite ends; the time cap is only a backstop.
final class Driver: XCTestCase {
    private let springboard = XCUIApplication(bundleIdentifier: "com.apple.springboard")
    private let settings = XCUIApplication(bundleIdentifier: "com.apple.Preferences")
    private var app: XCUIApplication!
    private var appName = ""

    /// Alert buttons the driver taps, in preference order. Anything else is logged, not tapped.
    private let acceptButtons = ["Allow", "Open"]

    func testDrive() throws {
        continueAfterFailure = true
        let env = ProcessInfo.processInfo.environment
        let bundleId = env["CLEANROOM_BUNDLE_ID"] ?? "io.teak.app.unity.dev"
        appName = env["CLEANROOM_APP_NAME"] ?? ""
        let seconds = TimeInterval(env["CLEANROOM_DRIVER_SECONDS"] ?? "") ?? 1800
        app = XCUIApplication(bundleIdentifier: bundleId)
        log("config bundle=\(bundleId) app_name=\(appName.isEmpty ? "(none)" : appName) seconds=\(Int(seconds))")
        XCTAssertFalse(appName.isEmpty, "CLEANROOM_APP_NAME is required to recognize the app's notifications")
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

            if tapBanner() {
                backgroundSince = nil
                continue
            }

            if settings.state == .runningForeground {
                settingsSince = settingsSince ?? Date()
                if Date().timeIntervalSince(settingsSince!) > 3 {
                    log("Settings in foreground, returning to app")
                    app.activate()
                    settingsSince = nil
                }
            } else {
                settingsSince = nil
            }

            if isBackgrounded(app) {
                backgroundSince = backgroundSince ?? Date()
                // Banners go to Notification Center after a few seconds, so look there too, but not
                // every tick: each pull-down is slow and noisy.
                if Date().timeIntervalSince(backgroundSince!) > 8 &&
                    Date().timeIntervalSince(lastNotificationCenterCheck) > 10 {
                    lastNotificationCenterCheck = Date()
                    if tapNotificationCenterCell() {
                        backgroundSince = nil
                        continue
                    }
                }
            } else {
                backgroundSince = nil
            }

            Thread.sleep(forTimeInterval: 1)
        }
        log("time cap reached")
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

    private func tapBanner() -> Bool {
        guard let banner = notificationElement() else { return false }
        log("tap notification \"\(banner.label)\"")
        return tapCenter(of: banner)
    }

    private func tapNotificationCenterCell() -> Bool {
        log("app backgrounded with no banner, checking Notification Center")
        let top = springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.2, dy: 0.0))
        let down = springboard.coordinate(withNormalizedOffset: CGVector(dx: 0.2, dy: 0.7))
        top.press(forDuration: 0.1, thenDragTo: down)
        Thread.sleep(forTimeInterval: 1.5)

        if let cell = notificationElement() {
            log("tap notification \"\(cell.label)\"")
            return tapCenter(of: cell)
        }
        log("no notification from \(appName) in Notification Center")
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
