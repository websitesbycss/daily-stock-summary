# syntax=docker/dockerfile:1
# Build context: ./backend

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only, so this layer is cached until a dependency changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/DailyStockSummary.Core/DailyStockSummary.Core.csproj src/DailyStockSummary.Core/
COPY src/DailyStockSummary.Infrastructure/DailyStockSummary.Infrastructure.csproj src/DailyStockSummary.Infrastructure/
COPY src/DailyStockSummary.Api/DailyStockSummary.Api.csproj src/DailyStockSummary.Api/
RUN dotnet restore src/DailyStockSummary.Api/DailyStockSummary.Api.csproj

COPY src/ src/
RUN dotnet publish src/DailyStockSummary.Api/DailyStockSummary.Api.csproj \
    -c Release -o /app --no-restore

# The runtime image ships tzdata, which the API needs to bucket days in each exchange's own time zone. The API
# checks for it on startup and refuses to run without it.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# The base image already listens on port 8080 (ASPNETCORE_HTTP_PORTS), so no URL is set here.
ENV ASPNETCORE_ENVIRONMENT=Production

# Non-root user defined by the base image.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DailyStockSummary.Api.dll"]
