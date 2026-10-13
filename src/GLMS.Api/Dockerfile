# syntax=docker/dockerfile:1
# glms-backend-api: multi-stage build. Build context is the repository root (see docker-compose.yml).

# ---- Stage 1: restore (cached until a .csproj or nuget.config changes) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
COPY nuget.config ./
COPY src/GLMS.Shared/GLMS.Shared.csproj                 src/GLMS.Shared/
COPY src/GLMS.Core/GLMS.Core.csproj                     src/GLMS.Core/
COPY src/GLMS.Infrastructure/GLMS.Infrastructure.csproj src/GLMS.Infrastructure/
COPY src/GLMS.Api/GLMS.Api.csproj                       src/GLMS.Api/
RUN dotnet restore src/GLMS.Api/GLMS.Api.csproj

# ---- Stage 2: build + publish (re-runs only when source changes) ----
FROM restore AS publish
COPY src/GLMS.Shared/         src/GLMS.Shared/
COPY src/GLMS.Core/           src/GLMS.Core/
COPY src/GLMS.Infrastructure/ src/GLMS.Infrastructure/
COPY src/GLMS.Api/            src/GLMS.Api/
RUN dotnet publish src/GLMS.Api/GLMS.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Stage 3: runtime (ASP.NET runtime only, no SDK, runs as non-root) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    FileStorage__RootPath=/app/App_Data/agreements
RUN mkdir -p /app/App_Data/agreements && chown -R app:app /app/App_Data
COPY --from=publish /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "GLMS.Api.dll"]
