# Build context: ./backend (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until the project file changes.
COPY global.json ./
COPY Relay.Api/Relay.Api.csproj Relay.Api/
RUN dotnet restore Relay.Api/Relay.Api.csproj

COPY Relay.Api/ Relay.Api/
RUN dotnet publish Relay.Api/Relay.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app ./
# The base image listens on 8080 (ASPNETCORE_HTTP_PORTS) and provides the non-root 'app' user.
USER $APP_UID
ENTRYPOINT ["dotnet", "Relay.Api.dll"]
