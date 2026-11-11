@README.md

## Local Android build (no aws-vault)

`rake package:copy package:import config:all build:android:local install:android`

Generates `debug.keystore` on first run — no KMS or aws-vault needed. Swap `package:copy` for `package:download` to pull from CDN instead of `../teak-unity`.

## Google Play upload build

`PLAY_UPLOAD=true PLAY_VERSION_CODE=<n> aws-vault exec <profile> -- bundle exec rake package:download package:import build:android` → `teak-unity-cleanroom.aab` for `io.teak.sdk.unity.dev`, signed with the KMS upload keystore. `<n>` must be above the last Play upload. Under `build:android:local` the output is `teak-unity-cleanroom.debug-signed.aab`: a compile check only, never upload it (the first upload's key becomes the app's upload key).
