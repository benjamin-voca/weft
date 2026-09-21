# Weft — version control for Blender projects

Weft applies a **cleaner git** to Blender pipelines. Every change to an asset —
a character rig, a scene, a material library — becomes an immutable,
content-hashed snapshot with a field-level semantic diff. No staging area, no
branches, no 40-char SHAs. Rollback is a new commit that restores an old
snapshot (history is never rewritten), and **every render job is pinned to the
snapshot it used**, so broken frames can be blamed on the exact change that
introduced them.

## The VCS logic (the "better git" part)

| git                          | Weft                                                      |
| ---------------------------- | --------------------------------------------------------- |
| staging area                 | one draft per asset, auto-saved, always diffing HEAD      |
| branches / merges            | none — linear history per asset                           |
| delta reconstruction         | full snapshots per commit (`jsonb`)                       |
| SHA-1                        | 8-char content hash of the canonical JSON                 |
| hand-written commit messages | auto-generated from the semantic diff (override optional) |
| revert rewrites history      | rollback mints a new commit (`isRollback`, `restoredVersion`) |
| `git blame` for code         | renders pin the config version they were rendered with    |

A **production** (Coffee Run short film, Nordic Kitchen arch-viz, Ironvale game
assets are seeded) holds **assets**, which hold **commits** (immutable
snapshots) and **render jobs** (with status, duration, attempts and the pinned
version). A deterministic render-farm simulator stands in for real dispatch:
locked assets block renders, disabled outputs are rejected, transient GPU OOMs
are saved by retries, and a `"force": "fail"` in the job data always fails.

## Stack

- **Backend** — .NET 10 Minimal APIs, vertical slices per feature
  (`src/Weft.Api`), EF Core 10 + Npgsql, cookie auth (PBKDF2-hashed
  passwords), EF migrations, deterministic seeder (~270 renders, 21 commits
  across 7 assets / 3 productions).
- **Frontend** — React 19 + TypeScript + Vite 7 + Tailwind v4 + framer-motion
  (`client/`), custom SVG charts, no UI kit. Public landing page, login, and a
  protected console.
- **Docker** — `docker-compose.yml` modeled on the edukurs setup: `server`
  profile (app + prod Postgres) and `dev` profile (Postgres exposed on :6970).
  The multi-stage `Dockerfile` builds the client, publishes the API, and
  Kestrel serves the SPA + API from one container on :8080.

## Command line (`bldr`)

The web console has a terminal twin: `bldr`, a C# CLI over the same API.

```bash
bldr login
bldr use "Coffee Run"
bldr add Bella --source //characters/bella/bella_rig.blend
bldr edit Bella --samples 256 --enable turntable
bldr commit -m "firstrig"
bldr log Bella
bldr render Bella final
```

Full manual: [docs/CLI.md](docs/CLI.md). Build: `dotnet publish src/Weft.Cli -c Release -o ~/.bldr/bin`.

## Seeded account

| email              | password |
| ------------------ | -------- |
| admin@example.com  | admin    |

## Run it

Dev (hot reload):

```bash
docker compose --profile dev up -d db        # postgres on :6970
cd src/Weft.Api && ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --no-launch-profile --urls http://localhost:5015
cd client && npm install && npm run dev      # vite on :5173, proxies /api
```

Open http://localhost:5173 — the database seeds itself on first boot
(migrations apply automatically in Postgres mode).

Production container:

```bash
docker compose --profile server up --build   # app on :8080, db migrates + seeds on boot
```

## Tests

```bash
dotnet test                       # 28 backend tests: auth + VCS unit tests
                                  # + full API flows over in-memory SQLite
cd client && npm test             # 21 frontend tests: components + helpers
cd client && npx playwright test  # 12 E2E tests: landing/login/auth wall +
                                  # UI → API → PostgreSQL console flows
                                  # (start the API on :5015 first)
```

The integration suite boots the real `Program` pipeline against in-memory
SQLite (provider switched via `Database:Provider`), which caught a genuine
EF Core pitfall: adding a preset-Guid entity through a navigation collection
tracks it as *Modified* — commits are therefore registered explicitly.

## API surface

```
GET    /health                            # public
POST   /api/auth/login                    POST /api/auth/logout     GET /api/auth/me
GET    /api/outputs                       # render-output catalog
GET    /api/projects                      POST /api/projects
GET    /api/projects/{id}/stats           # totals, latency, 14-day series, health
GET    /api/projects/{id}/assets          POST /api/projects/{id}/assets
GET    /api/assets/{id}                   DELETE /api/assets/{id}
PUT    /api/assets/{id}/config            POST /api/assets/{id}/discard
POST   /api/assets/{id}/commit            # 409 when nothing to commit
GET    /api/assets/{id}/commits
GET    /api/assets/{id}/diff?from=v&to=v  # HEAD ↔ draft by default
POST   /api/assets/{id}/rollback          # restores a snapshot as a new commit
GET    /api/assets/{id}/renders           POST /api/assets/{id}/renders   # queue
POST   /api/renders/{id}/replay           # re-run against the current snapshot
```

All `/api` routes except `/api/auth/*` require the session cookie; anonymous
callers get `401`, never a redirect.
