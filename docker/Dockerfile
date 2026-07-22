# syntax=docker/dockerfile:1

# ---- Stage 1: build the React frontend ----
FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ---- Stage 2: build & publish the .NET backend ----
# Only the Api project (and its ProjectReferences to Core/Infrastructure) is restored/published -
# the solution file isn't needed for a container build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY backend/src/RegisterSummary.Api/RegisterSummary.Api.csproj ./backend/src/RegisterSummary.Api/
COPY backend/src/RegisterSummary.Core/RegisterSummary.Core.csproj ./backend/src/RegisterSummary.Core/
COPY backend/src/RegisterSummary.Infrastructure/RegisterSummary.Infrastructure.csproj ./backend/src/RegisterSummary.Infrastructure/
RUN dotnet restore ./backend/src/RegisterSummary.Api/RegisterSummary.Api.csproj

COPY backend/src/ ./backend/src/
RUN dotnet publish ./backend/src/RegisterSummary.Api/RegisterSummary.Api.csproj -c Release -o /app/publish --no-restore

COPY --from=frontend-build /src/frontend/dist/ /app/publish/wwwroot/

# ---- Stage 3: runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd --system --gid 5678 appgroup \
    && useradd --system --uid 5678 --gid appgroup appuser

COPY --from=backend-build /app/publish ./

USER appuser
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "RegisterSummary.Api.dll"]
