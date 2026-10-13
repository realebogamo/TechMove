# syntax=docker/dockerfile:1
# glms-frontend-web: multi-stage build. Build context is the repository root (see docker-compose.yml).
# Only GLMS.Shared and GLMS.Web are copied in; the front end has no access to the data layer.

# ---- Stage 1: restore (cached until a .csproj or nuget.config changes) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
COPY nuget.config ./
COPY src/GLMS.Shared/GLMS.Shared.csproj src/GLMS.Shared/
COPY src/GLMS.Web/GLMS.Web.csproj       src/GLMS.Web/
RUN dotnet restore src/GLMS.Web/GLMS.Web.csproj

# ---- Stage 2: build + publish ----
FROM restore AS publish
COPY src/GLMS.Shared/ src/GLMS.Shared/
COPY src/GLMS.Web/    src/GLMS.Web/
RUN dotnet publish src/GLMS.Web/GLMS.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Stage 3: runtime (ASP.NET runtime only, runs as non-root) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeysPath=/app/keys
RUN mkdir -p /app/keys && chown -R app:app /app/keys
COPY --from=publish /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "GLMS.Web.dll"]
