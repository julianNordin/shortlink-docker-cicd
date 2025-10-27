# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Only the project file first. This layer's cache key is the .csproj alone, so editing C#
# does not re-download every NuGet package - which is the whole reason for the split.
COPY ShortLink.Api/ShortLink.Api.csproj ShortLink.Api/
RUN dotnet restore ShortLink.Api/ShortLink.Api.csproj

COPY ShortLink.Api/ ShortLink.Api/
RUN dotnet publish ShortLink.Api/ShortLink.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ---- runtime ----
# aspnet, not sdk: the runtime image is a fraction of the size and carries no compiler,
# no NuGet cache, and no source. alpine on top of that, which also means a much smaller
# package surface to carry CVEs.
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# `app` is the non-root user that ships with the .NET runtime images. Running as root in a
# container is the default and is worth undoing.
USER app

# `+` binds every interface. Binding to localhost inside a container means the port is only
# reachable from within that container, so published ports connect to nothing.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# busybox wget, which alpine already ships - so unlike the Debian runtime there is no
# package to install just to ask /health a question. --spider discards the body and still
# fails on a non-2xx status.
#
# start-period covers the startup migration, which on a cold volume takes a few seconds -
# failures before it elapses do not count against retries. localhost works here because the
# check runs inside the container's own network namespace.
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget --quiet --spider http://localhost:8080/health || exit 1

# Exec form, not shell form. Shell form would run this under /bin/sh -c, making sh PID 1;
# sh does not forward SIGTERM to its child, so `docker compose down` would wait out the full
# 10s grace period and then SIGKILL the app mid-request. In exec form dotnet is PID 1 and
# ASP.NET Core shuts down gracefully - measured at ~1.3s.
ENTRYPOINT ["dotnet", "ShortLink.Api.dll"]
