# Pass It On

Pass It On is a mobile-first platform for exchanging kids clothing and other kid items within and between daycares.

## Repository layout

- `apps/web` public React application
- `apps/admin` admin React application
- `apps/api` ASP.NET Core API and backend domain
- `docs` architecture and contract notes
- `infra` local infrastructure and operational assets
- `Requirements` original project requirements and reference assets

## Getting started

### Prerequisites

- Node.js 20+
- npm 10+
- .NET SDK 10.0+
- Docker Desktop or compatible Docker runtime

### Local development

1. Install frontend dependencies with `npm install` from the repository root.
2. Copy `.env.example` to `.env`.
3. Start PostgreSQL with `docker compose up -d db`.
4. Start the public app with `npm run dev:web`.
5. Start the admin app with `npm run dev:admin`.
6. Start the API with `dotnet run --project apps/api/src/PassItOn.Api/PassItOn.Api.csproj`.

When the API runs in `Development`, it applies pending EF Core migrations. If `ADMIN_SEED_ENABLED`, `ADMIN_SEED_EMAIL`, and `ADMIN_SEED_PASSWORD` are set in `.env`, it also ensures one local admin account exists using those values.

These values now come from `.env`, which the API loads automatically for local `dotnet run` and Docker Compose.

Default local URLs:

- Public app: `http://localhost:5173`
- Admin app: `http://localhost:5174`
- API health: `http://localhost:5200/health`
- PostgreSQL: `localhost:5432`

### Docker compose full stack

Run the whole application stack (database, API, public app, admin app):

1. Copy `.env.example` to `.env`.
2. Build and start everything with `docker compose up --build`.
3. Sign in to the admin app with the credentials you configured in `.env`.

Default service URLs:

- Public app: `http://localhost:5173`
- Admin app: `http://localhost:5174`
- API: `http://localhost:5200/health`
- PostgreSQL: `localhost:5432`

### Reverse proxy deployment

For deployment behind Nginx, set these values in `.env` before building the frontend containers:

- `WEB_PUBLIC_URL=https://passiton.nilscreque.com`
- `ADMIN_PUBLIC_URL=https://admin.passiton.nilscreque.com`
- `WEB_VITE_API_URL=/api`
- `ADMIN_VITE_API_URL=/api` when the admin UI is on its own host that also proxies `/api`, or a full API URL if you expose it differently

The ready-to-apply Nginx site examples live in `docs/nginx`.

### Validation

Use these checks after setup to confirm the documented workflow is working:

- `npm run build:web`
- `npm run build:admin`
- `dotnet build apps/api/src/PassItOn.Api/PassItOn.Api.csproj`
- Open `http://localhost:5200/health` after starting the API locally or through Docker Compose

## Current scaffold status

This repository currently contains the approved monorepo structure, local infrastructure, implemented public/admin API surfaces, both frontends wired to those endpoints, and an AWS S3-backed listing image upload pipeline (presigned upload URL + attach/delete APIs).

## AWS image pipeline configuration

The API now supports direct-to-S3 uploads for listing images:

1. Request upload URL: `POST /api/listings/{listingId}/images/upload-url`
2. Upload file to S3 using the returned presigned `uploadUrl`
3. Attach uploaded image: `POST /api/listings/{listingId}/images`

Configure these values in `.env`:

- `IMAGE_STORAGE_ENABLED`
- `IMAGE_STORAGE_BUCKET_NAME`
- `IMAGE_STORAGE_REGION`
- `IMAGE_STORAGE_PUBLIC_BASE_URL` (optional; use CloudFront/base CDN URL if available)
- `IMAGE_STORAGE_KEY_PREFIX`
- `IMAGE_STORAGE_MAX_FILE_SIZE_BYTES`
- `IMAGE_STORAGE_MAX_IMAGES_PER_LISTING`
- `IMAGE_STORAGE_PRESIGNED_URL_MINUTES`
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_SESSION_TOKEN` (optional)
