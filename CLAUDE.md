@README.md

## Local Android build (no aws-vault)

`rake package:copy package:import config:all build:android:local install:android`

Generates `debug.keystore` on first run — no KMS or aws-vault needed. Swap `package:copy` for `package:download` to pull from CDN instead of `../teak-unity`.
