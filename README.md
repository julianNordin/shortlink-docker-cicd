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

Running instructions land in Phase 03 (dev database) and Phase 08 (whole stack in one command).

## Project structure

```
ShortLink.sln
ShortLink.Api/            # the minimal API
ShortLink.Api.Tests/      # xUnit unit + integration tests
```

## Roadmap

- [x] Phase 01 — Solution scaffold, test project, README
- [ ] Phase 02 — Domain model and short-code generation
- [ ] Phase 03 — Dev PostgreSQL via Docker Compose
- [ ] Phase 04 — EF Core + Npgsql persistence and first migration
- [ ] Phase 05 — API endpoints: shorten, redirect, click counter, stats
- [ ] Phase 06 — Unit tests and Testcontainers integration tests
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
