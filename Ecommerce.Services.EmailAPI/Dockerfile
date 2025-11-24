# Multi-stage build for EmailAPI (Background Service)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy both EmailAPI and MessageBus project files
COPY ["Ecommerce.Services.EmailAPI/Ecommerce.Services.EmailAPI.csproj", "Ecommerce.Services.EmailAPI/"]
COPY ["Ecommerce.MessageBus/Ecommerce.MessageBus.csproj", "Ecommerce.MessageBus/"]
RUN dotnet restore "Ecommerce.Services.EmailAPI/Ecommerce.Services.EmailAPI.csproj"

# Copy remaining source files and build
COPY . .
WORKDIR "/src/Ecommerce.Services.EmailAPI"
RUN dotnet build "Ecommerce.Services.EmailAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ecommerce.Services.EmailAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .

# Background service health check (less frequent)
HEALTHCHECK --interval=60s --timeout=5s --start-period=20s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Ecommerce.Services.EmailAPI.dll"]
