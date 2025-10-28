# shortlink-docker-cicd

[![CI](https://github.com/julianNordin/shortlink-docker-cicd/actions/workflows/ci.yml/badge.svg)](https://github.com/julianNordin/shortlink-docker-cicd/actions/workflows/ci.yml)
[![GHCR](https://img.shields.io/badge/ghcr.io-shortlink--docker--cicd-2496ED?logo=docker&logoColor=white)](https://github.com/julianNordin/shortlink-docker-cicd/pkgs/container/shortlink-docker-cicd)

A deliberately tiny URL shortener, built as the vehicle for a real containerisation and CI/CD
setup: multi-stage Docker build, Docker Compose, PostgreSQL, and a GitHub Actions pipeline that
publishes images to GitHub Container Registry.

**Status:** ✅ Complete — all 16 phases. See [Roadmap](#roadmap) below.

## Why this project

The application is small on purpose. `POST` a URL, get a short code back; `GET /{code}` redirects
and bumps a click counter. That is the whole feature set — roughly a hundred lines of C#.

Everything interesting lives around the app rather than inside it: how it is built into an image,
how it talks to a database that only exists as a container, and how a push to `main` gets it
tested, built, and published without anyone touching a terminal.

## Tech stack

| Layer | Choice |
|---|---|
| Framework | ASP.NET Core Minimal API (.NET 9) |
| Data access | EF Core + Npgsql |
| Database | PostgreSQL 16 (containerised) |
| Container | Multi-stage `Dockerfile`, non-root runtime |
| Orchestration | Docker Compose (api + db) |
| CI/CD | GitHub Actions → GitHub Container Registry |
| Testing | xUnit, with Testcontainers for real-Postgres integration tests |

## Architecture

```
                                          +-----------------------------+
   client  --- POST /api/urls ------->    |                             |
           <-- 201 + short link ------    |   ShortLink.Api             |
                                          |   (Minimal API)             |
                                          |                             |
   browser --- GET /{code} ---------->    |   UrlService                |
           <-- 302 to target ---------    |     +- Base62 generator     |
                                          |     +- URL validator        |
                                          +-------------+---------------+
                                                        |  EF Core + Npgsql
                                          +-------------+---------------+
                                          |   PostgreSQL 16             |
                                          |   ShortUrls                 |
                                          |   unique index on Code      |
                                          +-----------------------------+
```

Both boxes are containers; `docker compose up` starts them together. The API applies its own
migrations at startup, so the database schema arrives with the app rather than as a separate
deployment step.

Three decisions carry most of the weight:

- **Codes are random Base62, not an encoded row id.** Sequential codes would let anyone walk
  the entire table by counting up from `1`. Seven characters gives 62^7 (~3.5 × 10^12), and
  the unique index on `Code` turns the rare collision into a retried insert.
- **The URL scheme is an allow-list**, absolute `http`/`https` only. Without it the service
  happily shortens `javascript:` and `data:` URIs, and `UriKind.Absolute` additionally blocks
  relative paths like `/admin` that would redirect back into the app.
- **Clicks are counted by the database**, with `UPDATE ... SET ClickCount = ClickCount + 1`
  rather than a load-modify-save, which silently drops clicks whenever two redirects overlap.

## Getting started

**Prerequisites:** Docker Desktop. That is the whole list — the API is compiled inside the
image, so no .NET SDK is needed just to run this.

```bash
git clone https://github.com/julianNordin/shortlink-docker-cicd.git
cd shortlink-docker-cicd
docker compose up --build
```

That builds the API image, starts PostgreSQL, waits for it to report healthy, then runs the API
against it on <http://localhost:8080>. The schema is created on first start.

```bash
# Shorten a URL — 201, with the short link in the body and Location pointing at its stats.
curl -i -X POST localhost:8080/api/urls -H 'Content-Type: application/json' -d '{"url":"https://example.com/a/long/page"}'

# Follow it — 302 to the target, and the click is counted.
curl -i localhost:8080/<code>

# Stats for a code — reading these does not itself count as a click.
curl -s localhost:8080/api/urls/<code>
```

### Endpoints

| Method | Path | Behaviour |
|---|---|---|
| `POST` | `/api/urls` | 201 + `Location`, or 400 `ProblemDetails` for anything that is not an absolute http(s) URL |
| `GET` | `/{code}` | 302 to the target and counts the click; 404 if unknown |
| `GET` | `/api/urls/{code}` | Stats for the code; 404 `ProblemDetails` if unknown |

## Working on the app

With the .NET 9 SDK on the host you can run the API directly and keep only the database in a
container — quicker to iterate on, and the debugger attaches normally:

```bash
docker compose up -d db                # just PostgreSQL
dotnet run --project ShortLink.Api     # reads appsettings.Development.json
dotnet test                            # unit tests + integration tests on a throwaway container
```

`dotnet test` starts its own PostgreSQL container through Testcontainers, so it neither needs
nor touches the Compose database.

### The database container

First start takes a few seconds while `initdb` creates the data directory. The service has a
`pg_isready` healthcheck, so Compose can tell you when it is genuinely ready rather than merely
started:

```bash
docker compose ps                                  # STATUS column shows (healthy)
docker compose exec -T db pg_isready -U shortlink  # or ask Postgres directly
docker compose logs -f db                          # follow the logs
```

The user, password, and database name all default to `shortlink`, on port 5432. To change any of
them, copy `.env.example` to `.env` and edit it — Compose reads that file automatically:

```bash
cp .env.example .env
```

Data lives in a named volume rather than a host directory, because Postgres needs Linux file
ownership that a Windows folder cannot provide. It survives restarts:

```bash
docker compose down     # stop everything, keep the data
docker compose down -v  # also delete the volume; next start is an empty database
```

## Published image

Every push to `main` that passes its tests publishes an image to GitHub Container Registry:

```bash
docker pull ghcr.io/juliannordin/shortlink-docker-cicd:latest
```

Tags are `latest` (default branch only), `sha-<short>` for a specific commit, and the branch
name. The owner is lowercased because GHCR rejects mixed-case image names.

The image needs a database — point it at one with `ConnectionStrings__Default`:

```bash
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Default="Host=<host>;Port=5432;Database=shortlink;Username=shortlink;Password=shortlink" \
  ghcr.io/juliannordin/shortlink-docker-cicd:latest
```

## The pipeline

```
   push to main --+
                  +--> +------------------+     +----------------------+
   pull request --+    |  Build and test  |---->|     Build image      |
                       |                  |     |                      |
                       |  restore         |     |  buildx + gha cache  |
                       |  build           |     |  metadata-action     |
                       |  test            |     |    tags              |
                       |    +- unit       |     |                      |
                       |    +- Testcont-  |     |  push to GHCR        |
                       |       ainers pg  |     |    -- main only --   |
                       +--------+---------+     +----------------------+
                                |
                                +--> test-results.trx artifact
                                     + job summary
```

Things worth pointing out:

- **The integration tests run against real PostgreSQL in CI**, not a stubbed provider.
  Testcontainers starts its own database, and `ubuntu-latest` already runs Docker, so no
  `services:` block is needed.
- **The image job is gated on the test job** with `needs:`, so a red run cannot produce an
  image that looks releasable.
- **Pull requests build the image but never push it.** A proposal should not be able to
  overwrite a published tag.
- **Superseded runs cancel**, keyed per ref, so a second push does not leave the first run
  building an image nobody will use.

## Branch protection

`main` is protected. The rules that matter:

| Rule | Why |
|---|---|
| Require a pull request | No direct pushes to `main`, including by the repository owner |
| Require the `Build and test` check | A red pipeline blocks the merge button |
| Require linear history | Keeps `git log --graph` a straight line |
| Rebase and squash merges only | Merge commits are disabled, which is what enforces the above |

Linear history is not a stylistic preference here. The history-rewriting tooling this
repository is built with walks commits with a single parent each, and a merge commit breaks
that walk outright.

## Project structure

```
ShortLink.sln
ShortLink.Api/
  Program.cs                    # the whole API surface: 3 endpoints + /health
  Contracts/                    # request and response shapes, kept off the entity
  Domain/ShortUrl.cs
  Services/                     # code generation, URL validation, the EF-backed store
  Data/                         # AppDbContext, DbInitializer, Migrations/
ShortLink.Api.Tests/
  ShortCodeGeneratorTests.cs    # unit
  UrlValidationTests.cs         # unit
  ApiFactory.cs                 # WebApplicationFactory + Testcontainers PostgreSQL
  EndpointTests.cs              # integration, against a real database
Dockerfile                      # multi-stage, alpine runtime, non-root, HEALTHCHECK
.dockerignore
docker-compose.yml              # api + db, api gated on the db healthcheck
.env.example
.github/
  workflows/ci.yml              # restore -> build -> test -> build image -> push GHCR
  dependabot.yml
  pull_request_template.md
  CODEOWNERS
```

## Roadmap

- [x] Phase 01 — Solution scaffold, test project, README
- [x] Phase 02 — Domain model and short-code generation
- [x] Phase 03 — Dev PostgreSQL via Docker Compose
- [x] Phase 04 — EF Core + Npgsql persistence and first migration
- [x] Phase 05 — API endpoints: shorten, redirect, click counter, stats
- [x] Phase 06 — Unit tests and Testcontainers integration tests
- [x] Phase 07 — Multi-stage Dockerfile
- [x] Phase 08 — Whole stack in Docker Compose
- [x] Phase 09 — GitHub Actions: restore → build → test
- [x] Phase 10 — Integration tests running in CI
- [x] Phase 11 — Docker image built in CI
- [x] Phase 12 — Image published to GHCR
- [x] Phase 13 — Health checks and container ergonomics
- [x] Phase 14 — Branch protection and pull request flow
- [x] Phase 15 — Image hardening and dependency automation
- [x] Phase 16 — README, badges, final polish

## License

Personal learning/portfolio project — no license specified yet.
