# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository — teak-unity-cleanroom, the Unity SDK integration test/validation app. See the top-level `sdks/CLAUDE.md` for the cross-SDK suite overview and build commands.

## Signing & the aws-vault / KMS keystore

Building a signed APK (`rake build:android`) needs the dev upload keystore, stored KMS-encrypted in the repo. `with_kms_decrypt` (Rakefile) decrypts it just before signing and deletes it after.

**aws-vault is ONLY for that KMS decrypt.** `package:download`/`package:import` pull the SDK from the CDN/S3 and need no credentials — they run fine without vault (you'll see harmless `Unable to locate credentials` / `enc: Option -k` noise from the `KMS_KEY` constant at Rakefile load; ignore it).

**Never run aws-vault detached/in the background.** It blocks on a macOS keychain prompt that can't surface in a non-interactive shell and hangs indefinitely (~80 min lost to this once). Run it foreground/interactively, or use the no-vault path below.

### Canonical signed build (with vault, interactive)

```
FL_TEAK_SDK_VERSION=<ver> USE_FACEBOOK=false \
  aws-vault exec alex --duration=12h -- \
  rake package:download package:import config:all build:android install:android
```

### No-vault path (local QA — the signing identity is irrelevant at runtime)

The key only has to exist and pass the Rakefile `keytool` check; the app behaves identically regardless of who signs.

1. Self-gen a debug keystore at the repo root:

   ```
   keytool -genkeypair -keystore io.teak.app.unity.dev.upload.keystore \
     -alias alias_name -storepass pointless -keypass pointless \
     -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Teak Test"
   ```

2. Locally (uncommitted) short-circuit `with_kms_decrypt` to just `yield file` — skip the KMS decrypt and the post-build `File.delete` so the keystore survives.

3. Build (mind the rvm gemset — a non-interactive shell won't auto-switch):

   ```
   source ~/.rvm/scripts/rvm && rvm use ruby-3.1.6@teak-unity-cleanroom
   FL_TEAK_SDK_VERSION=<ver> USE_FACEBOOK=false ANDROID_HOME="$HOME/Library/Android/sdk" \
     bundle exec rake package:download package:import config:all build:android install:android
   ```

Revert the Rakefile edit + remove the keystore when done.
