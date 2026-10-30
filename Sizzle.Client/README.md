# Sizzle.Client

Angular frontend for the Sizzle API.

## Run

1. Start the API from the repository root: `docker compose up -d` (API on http://localhost:8080, Swagger at `/swagger`).
2. In this folder: `npm install` and then `npm start`. Open http://localhost:4200.

The dev server proxies `/api`, `/hubs` (SignalR) and `/uploads` to `http://localhost:8080` (see `proxy.conf.json`), so the API does not need CORS.

## Google sign-in

1. Copy `Sizzle.WebApi/appsettings.Secrets.example.json` to `Sizzle.WebApi/appsettings.Secrets.json` (git-ignored) and paste the Google client secret.
2. The secrets file sets `GoogleOAuth:RedirectUri` to `http://localhost:4200/auth/google-callback`. Add this URI under **Authorized redirect URIs** for the OAuth client in Google Cloud Console.

Flow: the "Continue with Google" button opens `/api/Users/google-login`. Google sends the browser back to `/auth/google-callback` in the SPA, which passes `code` and `state` to `/api/Users/google-callback` and stores the returned JWT.

## Test accounts

When `TestData:Enabled` is on, the seeded users from `Sizzle.Infrastructure/TestData/app-users.json` work, for example `olivia@example.test` / `DemoKitchen!2026`.
