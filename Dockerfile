# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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
# no NuGet cache, and no source.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# The runtime image ships no HTTP client at all - no curl, no wget - and HEALTHCHECK needs
# one to ask the app whether it is actually healthy rather than merely running. Installed
# here while still root, because USER app comes below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# `app` is the non-root user that ships with the .NET runtime images. Running as root in a
# container is the default and is worth undoing.
USER app

# `+` binds every interface. Binding to localhost inside a container means the port is only
# reachable from within that container, so published ports connect to nothing.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# start-period covers the startup migration, which on a cold volume takes a few seconds -
# failures before it elapses do not count against retries. localhost works here because the
# check runs inside the container's own network namespace.
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ShortLink.Api.dll"]
