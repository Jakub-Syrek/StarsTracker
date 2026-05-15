# syntax=docker/dockerfile:1.6
#
# StarsTracker.Api production image.
#
# Multi-stage build:
#   1. sdk   — restore + publish into /app/publish
#   2. final — minimal aspnet runtime image with the publish output
#
# Designed for Railway / Fly.io / Render / any container host. Listens on
# $PORT when the host injects one, otherwise falls back to 8080.

ARG DOTNET_VERSION=10.0

# ---------------------------------------------------------------------------
# Stage 1: build & publish
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
WORKDIR /src

# Copy only csproj files first so docker caches the restore layer when only
# source code (not deps) changes. Mirrors the solution layout.
COPY StarsTracker.Shared/StarsTracker.Shared.csproj StarsTracker.Shared/
COPY StarsTracker.Core/StarsTracker.Core.csproj     StarsTracker.Core/
COPY StarsTracker.Api/StarsTracker.Api.csproj       StarsTracker.Api/
RUN dotnet restore StarsTracker.Api/StarsTracker.Api.csproj

# Now the rest of the source. Tests + MAUI project are excluded via
# .dockerignore so they don't bloat the build context or trigger restores.
COPY StarsTracker.Shared/ StarsTracker.Shared/
COPY StarsTracker.Core/   StarsTracker.Core/
COPY StarsTracker.Api/    StarsTracker.Api/

RUN dotnet publish StarsTracker.Api/StarsTracker.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# ---------------------------------------------------------------------------
# Stage 2: runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS final
WORKDIR /app

# Microsoft's aspnet:10.0-alpine ships with a non-root `app` user pre-created
# (UID 1654). Re-running addgroup/adduser would fail with "already exists",
# so we just chown the publish output to that user and switch to it.
COPY --from=build --chown=app:app /app/publish .
USER app

ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
EXPOSE 8080

ENTRYPOINT ["dotnet", "StarsTracker.Api.dll"]
