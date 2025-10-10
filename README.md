# shortlink-docker-cicd

A deliberately tiny URL shortener, built as the vehicle for a real containerisation and CI/CD
setup: multi-stage Docker build, Docker Compose, PostgreSQL, and a GitHub Actions pipeline that
publishes images to GitHub Container Registry.

**Status:** 🚧 In progress. See [Roadmap](#roadmap) below.

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

## Getting started

**Prerequisites:** [.NET 9 SDK](https://dotnet.microsoft.com/download) and Docker Desktop.

```bash
git clone https://github.com/julianNordin/shortlink-docker-cicd.git
cd shortlink-docker-cicd
dotnet build
dotnet test
```

Only PostgreSQL is containerised at this stage — the API still runs on the host with
`dotnet run`. Phase 08 moves the whole stack into Compose.

## Development database

Postgres runs as a container, so there is nothing to install on the host:

```bash
docker compose up -d db
```

First start takes a few seconds while `initdb` creates the data directory. The service has a
`pg_isready` healthcheck, so Compose can tell you when it is actually ready:

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
docker compose down     # stop the container, keep the data
docker compose down -v  # also delete the volume; next start is an empty database
```

## Project structure

```
ShortLink.sln
ShortLink.Api/            # the minimal API
ShortLink.Api.Tests/      # xUnit unit + integration tests
```

## Roadmap

- [x] Phase 01 — Solution scaffold, test project, README
- [x] Phase 02 — Domain model and short-code generation
- [x] Phase 03 — Dev PostgreSQL via Docker Compose
- [x] Phase 04 — EF Core + Npgsql persistence and first migration
- [x] Phase 05 — API endpoints: shorten, redirect, click counter, stats
- [x] Phase 06 — Unit tests and Testcontainers integration tests
- [ ] Phase 07 — Multi-stage Dockerfile
- [ ] Phase 08 — Whole stack in Docker Compose
- [ ] Phase 09 — GitHub Actions: restore → build → test
- [ ] Phase 10 — Integration tests running in CI
- [ ] Phase 11 — Docker image built in CI
- [ ] Phase 12 — Image published to GHCR
- [ ] Phase 13 — Health checks and container ergonomics
- [ ] Phase 14 — Branch protection and pull request flow
- [ ] Phase 15 — Image hardening and dependency automation
- [ ] Phase 16 — README, badges, final polish

## License

Personal learning/portfolio project — no license specified yet.
