# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching): copy project files only.
COPY HomeStock.slnx ./
COPY src/HomeStock.Domain/HomeStock.Domain.csproj src/HomeStock.Domain/
COPY src/HomeStock.Application/HomeStock.Application.csproj src/HomeStock.Application/
COPY src/HomeStock.Infrastructure/HomeStock.Infrastructure.csproj src/HomeStock.Infrastructure/
COPY src/HomeStock.Web/HomeStock.Web.csproj src/HomeStock.Web/
COPY tests/HomeStock.Tests/HomeStock.Tests.csproj tests/HomeStock.Tests/
RUN dotnet restore src/HomeStock.Web/HomeStock.Web.csproj

# Copy the rest and publish.
COPY . .
RUN dotnet publish src/HomeStock.Web/HomeStock.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Run as the non-root user provided by the base image.
USER $APP_UID

COPY --from=build /app/publish .

# Data (SQLite DB + attachments) lives on a mounted volume, outside the image.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=/data/homestock.db;Cache=Shared" \
    Storage__AttachmentsPath="/data/attachments"

EXPOSE 8080

# Container health check hits the readiness endpoint (DB + attachment storage).
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["/bin/sh", "-c", "wget -qO- http://127.0.0.1:8080/health/ready || exit 1"]

ENTRYPOINT ["dotnet", "HomeStock.Web.dll"]
