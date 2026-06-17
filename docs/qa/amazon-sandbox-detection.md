# QA — Amazon sandbox detection (getAppstoreSDKMode branch)

## What we're verifying

When a host app uses the **Amazon Appstore SDK** as its billing backend, it does not link the legacy Unity IAP v2.0 Amazon backend. The Appstore SDK ships its own `PurchasingService` class but omits the `IS_SANDBOX_MODE` static field that the legacy backend exposed. If Teak's sandbox detection only reads `IS_SANDBOX_MODE`, it gets a `NoSuchFieldException` and returns `false` unconditionally — misclassifying all sandbox purchases as production.

The fix adds a `getAppstoreSDKMode()` reflection branch (tried first) that reads the Appstore SDK's own detection API, with `IS_SANDBOX_MODE` as a fallback for legacy environments.

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
| **(a) Appstore SDK, no v2.0** — `USE_APPSTORE_SDK=true` | 1 (`getAppstoreSDKMode`) | **PRIMARY — exercises the getAppstoreSDKMode path** |
| (b) Legacy IAP v2.0 only — default | 3 (`IS_SANDBOX_MODE`) | Baseline |

---

## Prerequisites

- [ ] **Amazon App Tester** installed on the Fire device. Makes both branches report SANDBOX.
- [ ] App Tester IAP catalog (`amazon.sdktester.json`) with SKU `io.teak.app.sku.dollar` on the device.
- [ ] Fire device over adb, authorized (`adb devices` → `device`). ADB debugging + Unknown Sources on.
- [ ] teak-unity build available: `FL_TEAK_SDK_VERSION=<version> bundle exec rake package:download` or `bundle exec rake package:copy` from `../teak-unity`. Verify the bundled teak-android AAR contains the `getAppstoreSDKMode` branch before trusting results.
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

## Config (a) — Appstore SDK, no IS_SANDBOX_MODE (PRIMARY)

`USE_APPSTORE_SDK=true` does two things automatically:

1. Bumps `com.unity.purchasing` to v5.0.4 (which dropped Amazon entirely — no `AmazonAppStore.aar`, no `IS_SANDBOX_MODE`).
2. Adds `com.amazon.device:amazon-appstore-sdk:3.0.9` via EDM4U before `android:dependencies` resolves. The Appstore SDK bundles `PurchasingListener` + `PurchasingService` (without `IS_SANDBOX_MODE`) and `LicensingService.getAppstoreSDKMode()`.

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

**Without the fix:** no `getAppstoreSDKMode` branch → branch-3 `NoSuchFieldException` logged + `"sandboxMode":false` always, even under App Tester.

### What you will and won't see

With `UNITY_PURCHASING_V5` active, `SetupStorePlugin`'s `UnityPurchasing.Initialize` is compiled out entirely. Unity IAP never initializes — no `OnInitializeFailed`, no `ClassNotFoundException`. The Test Purchase button does not appear. The only expected signal is the `billing.amazon.v2` line from Teak's own reflection at SDK init.

### Caveat: getAppstoreSDKMode returns "UNKNOWN" until verifyLicense runs

Branch 1 may yield `"sandboxMode":false` if the cleanroom doesn't call `LicensingService.verifyLicense()`. If you see `false` under App Tester, confirm the `billing.amazon.v2` line is present (store activated) and check whether the app invokes `verifyLicense()`. This is a behavioral nuance of the Appstore SDK, not a Teak detection regression.

### Confirming the wiring

Use `strings` on extracted DEX. Two reliable discriminators:

```bash
APK=teak-unity-cleanroom.apk
mkdir -p /tmp/c845dex
unzip -o "$APK" 'classes*.dex' -d /tmp/c845dex

# Legacy Amazon IAP v2.0 linked — the lib's own init log (NOT present in Appstore SDK 3.x):
strings /tmp/c845dex/classes*.dex | grep "In-App Purchasing SDK initializing. SDK Version 2.0.76"

# Standalone Appstore SDK 3.x linked — type-descriptor form (dotted string is just an IPC intent target, ignore it):
strings /tmp/c845dex/classes*.dex | grep "Lcom/amazon/device/drm/LicensingService;"
```

| Signal | Baseline (b) | Appstore SDK (a) |
|---|---|---|
| `In-App Purchasing SDK initializing. SDK Version 2.0.76` | **present** | absent |
| `Lcom/amazon/device/drm/LicensingService;` | absent | **present** |

Notes:
- Bare strings `getAppstoreSDKMode` and `IS_SANDBOX_MODE` appear in **every** build — they're Teak's reflection-lookup strings, not lib presence signals. Ignore them.
- The dotted string `com.amazon.device.drm.LicensingService` also appears in the baseline (Unity IAP v4 uses it as an IPC intent target). Only the type-descriptor form (`Lcom/...;`) confirms the class is actually linked.
- Corroborate with the build log: `purchasing@4.1.2` + AmazonAppStore.aar present (baseline) vs `purchasing@5.0.4` + no AmazonAppStore.aar (appstore).

---

## After QA

The Rakefile restores `AdditionalDependencies.xml` and `AndroidResolverDependencies.xml` automatically in `ensure`. The next `rake build:android` run (without the toggle) re-resolves EDM4U deps and removes the Appstore SDK AAR from `Assets/Plugins/Android/`.

---

## Expected-result table

| Config | Branch | Under App Tester (SANDBOX) | Live build (no App Tester) |
|---|---|---|---|
| **(a) Appstore SDK, no v2.0** | 1 `getAppstoreSDKMode` | `"sandboxMode":true` (if verifyLicense called) | `"sandboxMode":false` |
| (b) IAP v2.0 only | 3 `IS_SANDBOX_MODE` | `"sandboxMode":true` | `"sandboxMode":false` |
