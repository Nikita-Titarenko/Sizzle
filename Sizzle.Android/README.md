# Sizzle Android

Android app for Sizzle (Kotlin + Jetpack Compose). Same features as the website, plus push notifications, share sheet, `sizzle://` deep links, and English/Ukrainian language switching.

## Running

Open `Sizzle.Android` in Android Studio and run `app`. Or from the terminal:

```bash
./gradlew installDebug
```

You need JDK 17 and Android SDK 35 (Android Studio comes with both).

Start the backend first (`docker compose up -d` in the repo root). The emulator reaches it at `http://10.0.2.2:8080/`.

## Config

Set in `gradle.properties`:

- `sizzle.apiUrl` - API address, default `http://10.0.2.2:8080/`
- `sizzle.webUrl` - website address used in share links
- `sizzle.googleWebClientId` - web OAuth client ID (same as `GoogleOAuth:ClientId` in the API). Leave empty to hide the Google button.

To test on a real phone, override the API URL with your PC's local IP in `~/.gradle/gradle.properties` so it doesn't end up in git:

```properties
sizzle.apiUrl=http://192.168.1.20:8080/
```

Debug builds allow HTTP, release builds are HTTPS only.

## Firebase (push notifications)

Optional - the app works without it, you just won't get pushes.

1. Add an Android app `com.sizzle.app` to the Firebase project and put `google-services.json` in `app/`.
2. In Firebase go to Settings > Service accounts > Generate new private key, save it as `Sizzle.WebApi/firebase-service-account.json`.
3. Add this to `Sizzle.WebApi/appsettings.Secrets.json`:
   ```json
   "Firebase": {
     "ProjectId": "your-project-id",
     "ServiceAccountFile": "firebase-service-account.json"
   }
   ```
4. Restart the API.

All three files are in `.gitignore`.

## Google sign-in

The web client ID and the Android OAuth client have to be in the **same** Google Cloud project (easiest is to just use the Firebase one - enabling Google in Firebase Authentication creates both).

The Android client needs the SHA-1 of the key your build is signed with. Get it with:

```bash
./gradlew signingReport
```

Careful: Android Studio installed via Flatpak/Snap uses its own debug keystore, not `~/.android/debug.keystore`, so its SHA-1 is different. Use whatever `signingReport` prints from the same Android Studio you run the app with. Each dev machine needs its own fingerprint added.

If sign-in just closes without doing anything, check logcat for `Auth.Api.Credentials`. `[16] Account reauth failed` almost always means the SHA-1 is wrong or missing.

## Project layout

```
app/src/main/java/com/sizzle/app/
  data/        API, models, session, settings
  push/        FCM
  auth/        Google sign-in
  ui/          theme, components, screens, navigation
```

Strings are generated from `tools/strings.py` - edit that file and run `python3 tools/strings.py` instead of editing `strings.xml` directly.
