# Avatisment

A social media web app built with **ASP.NET Core MVC (.NET 8)**, styled as a modern
three-column feed (sidebar nav · post feed · trends/suggestions), similar in spirit
to Twitter/X or Facebook but with its own purple↔cyan gradient identity.

## Run it

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cd Avatisment
dotnet restore
dotnet run
```

Then open the URL shown in the console (e.g. `http://localhost:5080`).

**Demo login:** `maria@avatisment.dev` / `demo1234`
(seed accounts are pre-verified so you can jump straight into the feed; new
sign-ups go through email + phone verification first — see below).

## Security & verification

- **Always starts at sign-in** — the app's default route is `/Account/Login`, and every page under `HomeController` requires an authenticated **and fully verified** account (a global action filter bounces anyone who isn't).
- **Email + phone verification** — registration now asks for a phone number alongside email. After signing up (or logging into an unverified account), you're walked through `/Account/VerifyEmail` then `/Account/VerifyPhone`, each with a 6-digit, 15-minute-lifetime one-time code and a resend option.
  - There's no real email/SMS provider wired up in this demo, so the generated code is shown directly on the verification page under "Demo mode" — swap in SendGrid/Twilio (or similar) in `AccountController` to send it for real.
- **Brute-force protection** — 5 failed logins for an email locks that account out for 10 minutes (`InMemoryDataStore.IsLockedOut` / `RegisterFailedLogin`).
- **Content verification** — all post, reel, comment, and message text is server-side sanitized (`InMemoryDataStore.SanitizeContent`): HTML/script tags stripped, whitespace trimmed, and length hard-capped regardless of what the client sent — in addition to Razor's automatic output encoding on render.
- **Cookie hardening** — the auth cookie is `HttpOnly`, `SameSite=Lax`, and `Secure` outside development; the antiforgery cookie is `HttpOnly` + `SameSite=Strict`. CSRF tokens are validated on every state-changing action.
- **Response headers** — `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and a `Referrer-Policy` are set on every response.
- **Password rules** — registration requires 8+ characters with at least one letter and one number, plus a confirm-password check.

## What's included

- **Auth** — cookie-based login/register with lockout protection, email + phone OTP verification (`AccountController`), SHA-256 password hashing for demo purposes.
- **Feed** — compose box (post **or** 🎬 reel), post/reel cards with like/comment/share, trending hashtags, "who to follow" — all AJAX-driven, in `wwwroot/js/site.js`.
- **Explore** — search people/posts or browse trending content and suggested people.
- **Notifications** — auto-generated on likes, comments, and new followers, with an unread badge in the sidebar.
- **Messages** — conversation list + threaded DMs, AJAX send. A "Message" button appears on someone's profile once you follow them.
- **Profiles** — cover gradient, avatar, bio, follower/following counts, follow/unfollow.
- **Design system** — CSS custom properties for color/radius/shadow tokens (`wwwroot/css/site.css`), Poppins + Inter type, fully responsive down to mobile.
- **In-memory data store** (`Services/InMemoryDataStore.cs`) — no database setup needed to try it out. Swap `IAvatismentDataStore` for an EF Core–backed implementation to persist data for real.

## Project layout

```
Avatisment/
├─ Controllers/       Account, Home (feed/profile + AJAX endpoints)
├─ Models/             AppUser, Post, Comment, view models
├─ Services/           IAvatismentDataStore + in-memory implementation
├─ Views/
│  ├─ Account/         Login, Register, VerifyEmail, VerifyPhone
│  ├─ Home/             Index (feed), Profile, Explore, Notifications, Messages, Error
│  └─ Shared/           _Layout (app shell), _AuthLayout (login/register/verify)
├─ wwwroot/
│  ├─ css/site.css      Design tokens + all component styles
│  └─ js/site.js        Like / comment / follow / message AJAX handlers
├─ Dockerfile, .dockerignore, docker-compose.yml   Container build + local run
├─ render.yaml, railway.json, fly.toml              Platform-specific deploy config
└─ .github/workflows/                               CI/CD: Azure deploy, GHCR image publish
```

## Deploy

The app is container-ready — a `Dockerfile` and platform config files are included
so you can go live on any of these without writing deployment code yourself.
**Important:** the in-memory data store resets on every restart/redeploy on all
of these until you swap in a real database (see "Next steps" below).

### Option A — Render.com (easiest, free tier)
1. Push this repo to GitHub.
2. On [render.com](https://render.com) → **New → Blueprint** → select the repo.
   Render detects `render.yaml` automatically and builds/deploys from the `Dockerfile`.
3. You get a live `https://avatisment.onrender.com`-style URL in a few minutes.

### Option B — Railway.app
1. Push this repo to GitHub.
2. On [railway.app](https://railway.app) → **New Project → Deploy from GitHub repo**.
   Railway reads `railway.json` and builds the `Dockerfile` automatically.
3. Add a public domain from the service's **Settings → Networking** tab.

### Option C — Fly.io
```bash
# one-time CLI install: https://fly.io/docs/flyctl/install/
flyctl auth login
flyctl launch --copy-config --name avatisment   # detects fly.toml
flyctl deploy
```

### Option D — Azure App Service (CI/CD via GitHub Actions)
1. In the Azure Portal, create an **App Service** (Linux, .NET 8, any tier — F1 is free).
2. In the App Service → **Get publish profile**, download it.
3. In your GitHub repo → **Settings → Secrets and variables → Actions**, add a secret
   named `AZURE_WEBAPP_PUBLISH_PROFILE` with that file's contents.
4. Edit `AZURE_WEBAPP_NAME` in `.github/workflows/azure-deploy.yml` to match your App
   Service's name.
5. Push to `main` — the included workflow builds and deploys automatically on every push.
   (Or trigger it manually from the Actions tab.)

### Option E — Any Docker host / your own VPS
```bash
docker build -t avatisment .
docker run -p 8080:8080 avatisment
# or: docker compose up --build
```
Then put Nginx or Caddy in front of it for TLS, or use a host that terminates
HTTPS for you. A `docker-publish.yml` workflow is also included that builds and
pushes the image to GitHub Container Registry (`ghcr.io`) on every push to `main`,
so any host that can pull a container image can run it without a local build step.

### Health check
All the configs above point to `GET /health`, which returns `200 OK` with a
JSON status payload — used for container/uptime health checks.

## Next steps for production

- Replace `InMemoryDataStore` with EF Core + a real database.
- Wire `AccountController`'s email/phone code generation to a real provider (SendGrid, SES, Twilio, etc.) instead of the demo on-screen code.
- Replace SHA-256 password hashing with `PasswordHasher<T>` (ASP.NET Core Identity) or a dedicated identity provider.
- Add real image/video upload (currently posts and reels use decorative CSS gradients instead of real media).
- Add pagination/infinite scroll to the feed instead of loading everything at once.
- Add a Content-Security-Policy header and rate-limit the verification/resend endpoints.
