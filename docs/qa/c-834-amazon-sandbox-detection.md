# QA — C-834: Amazon sandbox detection (getAppstoreSDKMode branch)

## What we're verifying

C-834: when a host app uses the Amazon Appstore SDK as its billing backend (no legacy IAP v2.0), `PurchasingService.IS_SANDBOX_MODE` doesn't exist and the old code returned `false` unconditionally. The fix adds a `getAppstoreSDKMode()` branch, tried first, with `IS_SANDBOX_MODE` as fallback.

Detection lives in `io.teak.sdk.store.AmazonSandbox.isSandboxMode()`. In order:

1. **Appstore SDK branch (the fix):** reflects `LicensingService.getAppstoreSDKMode()` → `"SANDBOX"/"PRODUCTION"/"UNKNOWN"`.
2. On `ClassNotFoundException | NoSuchMethodException` → falls through silently to branch 3.
3. **Legacy branch:** `PurchasingService.IS_SANDBOX_MODE`.
4. Any other exception → `Teak.log.exception(e)`, returns `false`.

The primary observable signal is in `io.teak.sdk.store.Amazon`'s constructor, which fires at SDK init (no purchase needed):

```
logcat tag: billing.amazon.v2
field:       sandboxMode: true/false
```

## Configs

| Config | Branch | Role |
|---|---|---|
| **(a) Appstore SDK, no v2.0** — `USE_APPSTORE_SDK=true` | 1 (`getAppstoreSDKMode`) | **PRIMARY — proves C-834** |
| (b) Legacy IAP v2.0 only — default | 3 (`IS_SANDBOX_MODE`) | Baseline |

---

## Prerequisites

- [ ] **Amazon App Tester** installed on the Fire device. Makes both branches report SANDBOX.
- [ ] App Tester IAP catalog (`amazon.sdktester.json`) with SKU `io.teak.app.sku.dollar` on the device.
- [ ] Fire device over adb, authorized (`adb devices` → `device`). ADB debugging + Unknown Sources on.
- [ ] teak-unity 4.3.13 build available: either `FL_TEAK_SDK_VERSION=4.3.13.beta0 bundle exec rake package:download` or `bundle exec rake package:copy` from `../teak-unity`. Verify the bundled teak-android AAR contains the `getAppstoreSDKMode` branch before trusting results.
- [ ] Tooling: Unity, Ruby + `bundle install`, `aws` CLI, `adb`.

---

## Config (b) — baseline (legacy IAP v2.0)

```bash
cd teak-unity-cleanroom
BUILD_TYPE=dev bundle exec rake package:download package:import config:all build:amazon install:amazon
```

Observe:

```bash
adb logcat -c
adb shell monkey -p io.teak.app.unity.dev -c android.intent.category.LAUNCHER 1
adb logcat | grep -i "billing.amazon.v2"
```

Expected (App Tester running): `billing.amazon.v2 ... "sandboxMode":true` via branch 3.

---

## Config (a) — Appstore SDK, no IS_SANDBOX_MODE (PRIMARY — proves C-834)

`USE_APPSTORE_SDK=true` does three things automatically:

1. Adds `com.amazon.device:amazon-appstore-sdk:3.0.9` via EDM4U before `android:dependencies` resolves. The Appstore SDK bundles `PurchasingListener` + `PurchasingService` (without `IS_SANDBOX_MODE`) and `LicensingService.getAppstoreSDKMode()`.
2. Moves Unity IAP's `AmazonAppStore.aar` from the PackageCache aside before the player build so its bundled `in-app-purchasing-2.0.76.jar` (which defines `IS_SANDBOX_MODE`) is absent from the APK. Both files are restored in `ensure`.
3. Gitignores the EDM4U-resolved Appstore SDK AAR (`Assets/Plugins/Android/com.amazon.device.*`) to prevent accidental commit.

```bash
BUILD_TYPE=dev USE_APPSTORE_SDK=true bundle exec rake config:all build:amazon install:amazon
```

Observe:

```bash
adb logcat -c
adb shell am start -n io.teak.app.unity.dev/com.unity3d.player.UnityPlayerActivity
adb logcat | grep -iE "billing.amazon.v2|getAppstoreSDKMode|exception"
```

**Expected (App Tester running, with the fix):** `billing.amazon.v2 ... "sandboxMode":true` via branch 1. No `NoSuchFieldException` / `Teak.log.exception` noise.

**Pre-fix (the bug):** no `getAppstoreSDKMode` branch existed → branch-3 `NoSuchFieldException` logged + `"sandboxMode":false` always.

### Known noise to ignore

Unity IAP auto-initializes at launch (`TestDriver.Awake → SetupStorePlugin`) with `AppStore.AmazonAppStore`. Because `AmazonAppStore.aar` is absent from the APK, Unity IAP's Amazon bridge will fail — expect `TestDriver: OnInitializeFailed` or `ClassNotFoundException` from Unity IAP in logcat. **This is expected and unrelated to Teak's detection.** The `billing.amazon.v2` line comes from Teak's own reflection at SDK init, which fires first and is independent of Unity IAP. Do not trigger a purchase in this config; the Test Purchase button will not function.

### Caveat: getAppstoreSDKMode returns "UNKNOWN" until verifyLicense runs

Branch 1 may yield `"sandboxMode":false` if the cleanroom doesn't call `LicensingService.verifyLicense()`. If you see `false` under App Tester, confirm the `billing.amazon.v2` line is present (store activated) and check whether the app invokes `verifyLicense()`. This is a behavioral nuance of the Appstore SDK, not a C-834 regression.

### Confirming the wiring

```bash
# Appstore SDK in APK (classes from 3.0.9):
unzip -l teak-unity-cleanroom.apk | grep -i "LicensingService\|appstore"

# IS_SANDBOX_MODE absent:
# (expects no output)
unzip -p teak-unity-cleanroom.apk classes*.dex | strings | grep IS_SANDBOX_MODE
```

---

## After QA

The Rakefile restores `AdditionalDependencies.xml` and the `AmazonAppStore.aar` automatically. `ProjectSettings/AndroidResolverDependencies.xml` is also restored in `ensure`. The next `rake build:android` run (without the toggle) re-resolves EDM4U deps and removes the Appstore SDK AAR from `Assets/Plugins/Android/`.

---

## Expected-result table

| Config | Branch | Under App Tester (SANDBOX) | Live build (no App Tester) |
|---|---|---|---|
| **(a) Appstore SDK, no v2.0** | 1 `getAppstoreSDKMode` | `"sandboxMode":true` (if verifyLicense called) | `"sandboxMode":false` |
| (b) IAP v2.0 only | 3 `IS_SANDBOX_MODE` | `"sandboxMode":true` | `"sandboxMode":false` |
