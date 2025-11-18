# Multi-stage build for Web MVC Frontend
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["E-commerce.Web/E-commerce.Web.csproj", "E-commerce.Web/"]
RUN dotnet restore "E-commerce.Web/E-commerce.Web.csproj"

# Copy remaining source files (includes wwwroot, Views, etc.)
COPY . .
WORKDIR "/src/E-commerce.Web"
RUN dotnet build "E-commerce.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "E-commerce.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .

# Health check for web app (root endpoint instead of /health)
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/ || exit 1

ENTRYPOINT ["dotnet", "E-commerce.Web.dll"]
