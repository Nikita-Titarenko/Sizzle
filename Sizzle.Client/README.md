# Sizzle.Client

Angular frontend for the Sizzle API.

## Run

### Everything in Docker

From the repository root:

```bash
docker compose up -d --build
```

| Service         | URL                           |
| --------------- | ----------------------------- |
| Web app         | http://localhost:4200         |
| API + Swagger   | http://localhost:8080/swagger |
| MySQL           | localhost:3306                |

The `sizzle-client` container builds the app and serves it with nginx (`nginx.conf`), which forwards `/api`, `/hubs` (SignalR WebSockets) and `/uploads` to the API container. After changing frontend code, rebuild it with `docker compose up -d --build sizzle-client`.

Stop everything with `docker compose down` (add `-v` to also wipe the database).

### Frontend with live reload

For day-to-day frontend work, run the API in Docker and the Angular dev server locally:

```bash
docker compose up -d mysql sizzle-webapi
cd Sizzle.Client
npm install
npm start
```

`ng serve` uses the same port (4200), so stop the client container first: `docker compose stop sizzle-client`. The dev server proxies the same paths to `http://localhost:8080` (see `proxy.conf.json`), so the API does not need CORS.

## Tests and checks

```bash
npm test              # unit tests in watch mode (Vitest)
npm run test:ci       # single run, as in CI
npm run format:check  # Prettier; `npm run format` fixes it
```

Unit tests cover the contracts that break silently: translation keys and placeholders, the
multipart field names the API binds (`Steps[0].Files`, ...), the `Accept-Language` header,
file limits mirrored from the API, the ingredient tree, JWT handling and the recipe editor's
validation.

GitHub Actions (`.github/workflows/ui.yml`) runs on every push and pull request that touches
`Sizzle.Client/`: formatting, unit tests (failures are annotated on the PR), a production build
with bundle budgets, and a Docker job that builds the image, validates `nginx.conf` and checks
that deep links are served by the SPA.

## Google sign-in

1. Copy `Sizzle.WebApi/appsettings.Secrets.example.json` to `Sizzle.WebApi/appsettings.Secrets.json` (git-ignored) and paste the Google client secret.
2. The secrets file sets `GoogleOAuth:RedirectUri` to `http://localhost:4200/auth/google-callback`. Add this URI under **Authorized redirect URIs** for the OAuth client in Google Cloud Console.

Flow: the "Continue with Google" button opens `/api/Users/google-login`. Google sends the browser back to `/auth/google-callback` in the SPA, which passes `code` and `state` to `/api/Users/google-callback` and stores the returned JWT.

## Test accounts

When `TestData:Enabled` is on, the seeded users from `Sizzle.Infrastructure/TestData/app-users.json` work, for example `olivia@example.test` / `DemoKitchen!2026`.

## Project structure

```
src/
├── environments/        API origin (empty = same origin via the dev proxy)
├── styles/              global SCSS partials: variables, base, buttons, forms, components, utilities
└── app/
    ├── core/            app-wide singletons, no UI
    │   ├── api/         one HTTP service per API area (users, recipes, comments, catalog)
    │   ├── auth/        JWT storage, route guard, HTTP interceptor
    │   ├── models/      TypeScript interfaces matching the API DTOs
    │   ├── services/    current user profile, SignalR notifications
    │   └── utils/
    ├── layout/          header and notification toasts shown on every page
    ├── features/        routed pages grouped by area (auth, feed, recipes, users)
    │                    plus components only that area uses (feed-item, comments-drawer, ingredient-picker)
    └── shared/          reusable components and pipes (recipe-card, comment-section, icon, user-avatar, ...)
```

The home page (`features/feed`) is a vertical, snap-scrolling feed with one recipe per screen: Shorts/Reels-style action rail (like, comments, share, open recipe, views), Instagram-style caption with author and follow button, double-tap to like, and category filters. Video covers (`.mp4`, `.webm`, `.mov`, ...) autoplay muted while on screen; images and recipes without media work the same way. Arrow keys or `j`/`k` move between posts on desktop. The searchable grid lives at `/explore`.

Every component lives in its own folder as `name.ts` + `name.html` + `name.scss`. Pages are lazy-loaded from `app.routes.ts`.
Format with `npx prettier --write "src/**/*.{ts,html,scss}"`.
