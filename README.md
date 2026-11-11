# Quickly Building #

`rake package:download package:import config:all build:android install:android`

Will find the best version of Unity it can, download the latest Teak SDK, configure a development build, then build the Android app and install it on any connected device.

## Verifying the UPM path ##

`script/verify-upm`

One command for the UPM half of the pre-release gate. It installs gems if needed, then runs `rake verify:upm` (`package:upm firebase:import config:all build:android:local`) and prints which `io.teak.unity.sdk` version UPM resolved. UPM only serves published versions, so promote an rc first and check that the printed version matches it.

## Unity Version ##

The `UNITY_HOME` environment variable sets which Unity editor will be used for building, e.g. `UNITY_HOME=/Applications/Unity/Hub/Editor/2020.3.47f1`

## AWS Credentials ##

If AWS credentials are managed by `aws-vault` then use `aws-vault exec <user> -- <rake command>`

## package ##

Package is one of:

* download
* copy
* build
* upm
* import

### download ###

Resets the cleanroom and downloads the Teak SDK from the CDN (using the Teak Fastlane Plugin). You can control the version which is downloaded using the `FL_TEAK_SDK_VERSION` environment variable.

By default it will download the latest Teak SDK.

### copy ###

Resets the cleanroom and copies the Teak SDK from a local directory (using the Teak Fastlane Plugin). You can control the path from which the SDK is copied using the `FL_TEAK_SDK_SOURCE` environment variable.

By default it will look in `../teak-unity`

### build ###

Resets the cleanroom and builds the Teak SDK locally. Without further arguments it will re-build both the Android and iOS native SDKs before building the Unity SDK. You can specify building a specific SDK with the following:

* `package:build:ios` builds the native iOS SDK, copies the existing Android SDK, builds the Unity SDK
* `package:build:android` builds the native Android SDK, copies the existing iOS SDK, builds the Unity SDK
* `package:build:unity` copies the native Android and iOS SDKs, builds the Unity SDK

This assumes that the iOS, Android and Unity repositories are located in `../teak-ios`, `../teak-android`, and `../teak-unity` respectively.

### upm ###

Resets the cleanroom and configures the build to install the Teak SDK from Teak's UPM git repository.

### import ###

Imports the Teak SDK located in the current directory, and then imports the Prime31 plugin or the UnityIAP plugin and additionally the Facebook SDK plugin.

By default it will import the UnityIAP plugin, the Facebook SDK plugin, and the bundled FirebaseMessaging.unitypackage.

You can control what is used with the following environment variables:

* `USE_UNITY_IAP` with 'true' or 'false' defaults to 'true'
* `USE_PRIME31` with 'true' or 'false' defaults to 'false'
* `FACEBOOK_SDK_VERSION` with a specific Facebook SDK for Unity version, defaults to '16.0.1'
* `USE_FACEBOOK` with 'true' or 'false' to control if Facebook is in the build, defaults to false

### config ###

Configures the build, there are subcommands to this, but you should just be using `config:all`.

By default it will configure a 'dev' build, you can specify the build type using `BUILD_TYPE` with one of:

* `dev` Development build and Teak credentials for Teak SDK - Dev
* `prod` Production build and credentials for Teak SDK - Prod
* `mismatch` A development build with mismatching Firebase credentials

## build ##

Performs the build.

* `build:ios` Builds and exports an iOS IPA. Note that if you want to run the build via Xcode, instead use `ios:build` and then open `Unity-iPhone/Unity-iPhone.xcworkspace`
* `build:android` Builds an Android APK
* `build:amazon` Build an Android APK designed for the Amazon store
* `build:webgl` Builds the WebGL package

For Android the following environment variables are available:

* `USE_IL2CPP_ON_ANDROID` with 'true' or 'false' defaults to 'true' (IL2CPP + arm64-v8a; set 'false' for a Mono/armeabi-v7a build)
* `TARGET_API` to set the targetSdkLevel for Android, defaults to 33

For iOS, `IOS_LIFECYCLE` picks the app lifecycle:

* `scene` (default) Unity's UIScene export as-is. The build fails if Unity stops emitting `UIApplicationSceneManifest`. Use on iOS 27.
* `legacy` removes `UIApplicationSceneManifest` for an app-delegate build. Use as the iOS 26 and earlier control; it traps at launch on the iOS 27 SDK.

`IOS_SDK` is `device` (default) or `simulator`; use `simulator` to export a project that builds for the iOS Simulator.

The installed app is named `<Lifecycle> <BuildType>` (e.g. `Scene Dev`) so you can tell which build you're running.

## install ##

Installs the build.

* `install:ios` uses `ideviceinstaller` to install the exported IPA
* `install:android` installs the build with the installer ref set to Google Play (`com.android.vending`)
* `install:google_play` same as `install:android`
* `install:amazon` installs the build with the installer ref set to the Amazon store (`com.amazon.venezia`)
* `install:webgl` runs an http server on local port `8000` hosting the WebGL build

## Running the suite unattended on iOS ##

`rake test:ios` installs the last iOS build fresh, runs the whole TestDriver suite with nobody touching the device, and prints every test's result. A small XCUITest driver (`ios-driver/`) answers the system UI the suite can't: the notification permission prompt, "Open in…?" alerts, notification banners and Notification Center, and returning from Settings.

* Device: `IOS_DEVICE=<udid> rake ios:build ios:device_app test:ios`.
  * There's no default device. Use the hardware UDID from `xcrun xctrace list devices`, not the CoreDevice identifier from `devicectl list devices`; xcodebuild only takes the UDID.
  * One-time setup on the device: Developer Mode on, Settings › Developer › Enable UI Automation on, Auto-Lock set to Never. The first driver run raises a UI Automation prompt on the device that someone has to accept; until then the driver times out "enabling automation mode".
  * The device has to be in the team's development profiles. `ALLOW_PROVISIONING_UPDATES=1` lets Xcode register it and create profiles on the shared Apple team; that's off by default.
  * `ios:device_app` signs with Xcode-managed profiles. `ios:fastlane` signs with match's profiles, which only carry devices match knows about; use it for builds that go out, not for this.
* Simulator: `IOS_SDK=simulator rake ios:build ios:simulator_app test:ios`, with one simulator booted (or `IOS_SIMULATOR=<udid>`).

If the app dies before the suite finishes, the run stops, the test it died in is marked Crashed, and the crash report path and faulting frames are printed. The rest are marked Not run. If no report can be found, it prints the signal the app died from instead (from the device console, or the simulator's log). `CRASH_AT="<test name>"` makes a debug build crash just before that test, to check this works.

Logs, the driver's result bundle and `results.json` go to `build/ios-suite/<time>/`. `script/ios-suite --help` has the options.

## deploy ##

Deploys the build.

* `deploy:webgl` uploads the WebGL build to Facebook hosting.
