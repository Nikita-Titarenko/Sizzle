# Sizzle.Client

Angular frontend for Sizzle.

## Running

Everything in Docker (from the repo root):

```bash
docker compose up -d --build
```

- Website: http://localhost:4200
- API / Swagger: http://localhost:8080/swagger
- MySQL: localhost:3306

nginx inside the client container proxies `/api`, `/hubs` and `/uploads` to the API. If you change frontend code, rebuild with `docker compose up -d --build sizzle-client`.

`docker compose down` stops everything, `docker compose down -v` also deletes the database.

### Dev server

For frontend work it's nicer to use `ng serve` with live reload:

```bash
docker compose stop sizzle-client
docker compose up -d mysql sizzle-webapi
cd Sizzle.Client
npm install
npm start
```

It runs on the same port (4200) and proxies API calls to `localhost:8080` (see `proxy.conf.json`).

## Tests

```bash
npm test              # watch mode
npm run test:ci       # single run
npm run format:check  # prettier (npm run format to fix)
```

CI (`.github/workflows/ui.yml`) runs formatting, tests, a production build and a Docker build on every push that touches `Sizzle.Client/`.

## Google sign-in

1. Copy `Sizzle.WebApi/appsettings.Secrets.example.json` to `appsettings.Secrets.json` and fill in the client secret.
2. Add `http://localhost:4200/auth/google-callback` to the OAuth client's authorized redirect URIs in Google Cloud.

## Test accounts

With `TestData:Enabled` turned on, the users from `Sizzle.Infrastructure/TestData/app-users.json` are seeded, e.g. `olivia@example.test` / `DemoKitchen!2026`.

## Structure

```
src/app/
  core/       api services, auth (jwt, guard, interceptor), models
  layout/     header, notifications
  features/   pages: auth, feed, recipes, users
  shared/     reusable components and pipes
```

Each component has its own folder with `.ts`, `.html` and `.scss`. Routes are lazy-loaded from `app.routes.ts`.
