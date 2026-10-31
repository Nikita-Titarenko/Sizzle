# Sizzle for Android

Native Android client for Sizzle, written in Kotlin with Jetpack Compose. It covers the same features as the website:

- the vertical "For you" feed with video autoplay and ingredient/category filters
- explore with search
- recipe pages with comments
- the recipe editor
- profiles, follows, and sign-in by email or Google

It also adds Android features:

- push notifications when someone you follow posts a recipe
- the system share sheet and the photo picker
- per-app language (English / Українська) and a dark/light theme
- `sizzle://` links

## Build and run

Open this folder (`Sizzle.Android`) in Android Studio and run the `app` configuration. From a terminal:

```bash
./gradlew assembleDebug        # app/build/outputs/apk/debug/app-debug.apk
./gradlew installDebug         # onto a running emulator or connected phone
```

Requires JDK 17 and Android SDK 35. Android Studio provides both.

## Which API the app talks to

The app reads the API address from `gradle.properties`:

| Property | Default | Meaning |
| --- | --- | --- |
| `sizzle.apiUrl` | `http://10.0.2.2:8080/` | Sizzle.WebApi. `10.0.2.2` is your computer as seen from the emulator. |
| `sizzle.webUrl` | `http://localhost:4200/` | Website address used in shared recipe links. |
| `sizzle.googleWebClientId` | the API's `GoogleOAuth:ClientId` | Google sign-in. If it is empty, the Google button is hidden. |

On a physical phone, use your computer's LAN address. Put it in `~/.gradle/gradle.properties` so the change stays local:

```properties
sizzle.apiUrl=http://192.168.1.20:8080/
```

Debug builds allow plain HTTP. Release builds allow HTTPS only.

## Push notifications (Firebase Cloud Messaging)

The app builds and runs without Firebase; it just gets no pushes. To turn them on:

1. In the [Firebase console](https://console.firebase.google.com/), create a project, or add Firebase to the Google Cloud project that already holds the OAuth client.
2. Add an Android app with package name `com.sizzle.app`. Download `google-services.json` into `Sizzle.Android/app/`. Git ignores this file.
3. Go to Project settings → Service accounts → Generate new private key. Save the key as `Sizzle.WebApi/firebase-service-account.json` (also ignored by git).
4. In `Sizzle.WebApi/appsettings.Secrets.json`, add:

   ```json
   "Firebase": {
     "ProjectId": "your-firebase-project-id",
     "ServiceAccountFile": "firebase-service-account.json"
   }
   ```

5. Restart the API and rebuild the app.

How it works:

- After sign-in, the app sends its FCM token to `POST /api/users/devices`.
- On sign-out, it removes the token with `DELETE /api/users/devices`.
- When someone posts a recipe, the API notifies their followers twice: over SignalR, for open web pages, and as an FCM data message to their phones.
- The app writes the notification text itself, in its current language. Tapping the notification opens the recipe.
- The API deletes tokens that FCM reports as no longer registered.

## Google sign-in

The app uses Credential Manager ("Sign in with Google"). The ID token it receives goes to `POST /api/users/google-token`, and the API verifies it against `GoogleOAuth:ClientId`.

In the same Google Cloud project as that web client ID, create an **Android** OAuth client:

- package name `com.sizzle.app`
- the SHA-1 of the key that signs the app

For debug builds, get the SHA-1 with:

```bash
keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android
```

Every developer machine and the release key each need their own SHA-1 entry. The emulator needs a system image with Google APIs.

## Code layout

```
app/src/main/java/com/sizzle/app/
  data/           API models, Retrofit API, session (JWT), settings, media uploads
  push/           FCM service, token registration, notifications
  auth/           Google sign-in
  ui/theme/       colors from the website's design tokens, both themes
  ui/components/  shared UI: loader, error/empty views, cards, comments, pickers, video
  ui/screens/     feed, explore, recipe, editor, profile, user, auth
  ui/navigation/  routes, bottom bar, sizzle:// links
```

Strings live in `tools/strings.py`, one table with both languages. After editing it, run `python3 tools/strings.py` to regenerate `res/values*/strings.xml`.
