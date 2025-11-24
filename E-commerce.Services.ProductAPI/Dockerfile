# Multi-stage build for ProductAPI
# Stage 1: Base runtime environment (ASP.NET Core 8.0)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Stage 2: Build environment (SDK 8.0 for compilation)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies (cached layer)
COPY ["E-commerce.Services.ProductAPI/E-commerce.Services.ProductAPI.csproj", "E-commerce.Services.ProductAPI/"]
RUN dotnet restore "E-commerce.Services.ProductAPI/E-commerce.Services.ProductAPI.csproj"

# Copy remaining source files and build
COPY . .
WORKDIR "/src/E-commerce.Services.ProductAPI"
RUN dotnet build "E-commerce.Services.ProductAPI.csproj" -c Release -o /app/build

# Stage 3: Publish optimized release artifacts
FROM build AS publish
RUN dotnet publish "E-commerce.Services.ProductAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: Final runtime image (minimal, production-ready)
FROM base AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .

# Health check endpoint (will be implemented in Phase 3)
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "E-commerce.Services.ProductAPI.dll"]
