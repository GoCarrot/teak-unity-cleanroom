@README.md

## Local Android build (no aws-vault)

`rake package:copy package:import config:all build:android:local install:android`

Generates `debug.keystore` on first run — no KMS or aws-vault needed. Swap `package:copy` for `package:download` to pull from CDN instead of `../teak-unity`.

## Google Play upload build

`PLAY_UPLOAD=true PLAY_VERSION_CODE=<n> aws-vault exec <profile> -- bundle exec rake package:download package:import build:android` → `teak-unity-cleanroom.aab` for `io.teak.sdk.unity.dev`, signed with the KMS upload keystore. `<n>` must be above the last Play upload. Under `build:android:local` the output is `teak-unity-cleanroom.debug-signed.aab`: a compile check only, never upload it (the first upload's key becomes the app's upload key).

## Start a test purchase from a launch link

The "Test Purchase" button lives in Unity's `SurfaceView`, so `uiautomator` can't see it. The `/purchase/:sku` route starts the same purchase (Unity IAP 4 and 5). If the product hasn't been fetched yet, the purchase starts as soon as it is.

`adb shell am start -a android.intent.action.VIEW -d 'teak<appId>:///purchase/io.teak.app.sku.dollar' io.teak.app.unity.dev`

`<appId>` is `teak_app_id` for the build type in the `Rakefile` (`613659812345256` for `dev`); the package is `io.teak.app.unity.dev` for `dev`. Cold start: `adb shell am force-stop io.teak.app.unity.dev` first. Already running: run it as is. Play's purchase sheet is native UI and shows in a `uiautomator` dump; logcat gets a `[TestDriver-UnityIAP] Purchase link:` line. If an earlier order on the test account was never consumed, Play shows "already owned" instead of the sheet.
