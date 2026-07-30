## syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Malluflix.Core/Malluflix.Core.csproj src/Malluflix.Core/
COPY src/Malluflix.Api/Malluflix.Api.csproj src/Malluflix.Api/
RUN dotnet restore src/Malluflix.Api/Malluflix.Api.csproj

COPY src/Malluflix.Core/ src/Malluflix.Core/
COPY src/Malluflix.Api/ src/Malluflix.Api/

RUN dotnet publish src/Malluflix.Api/Malluflix.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/* \
 && groupadd -r app && useradd -r -g app app
USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "Malluflix.Api.dll"]
