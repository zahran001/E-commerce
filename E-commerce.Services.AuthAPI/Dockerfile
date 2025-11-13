# Multi-stage build for AuthAPI
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["E-commerce.Services.AuthAPI/E-commerce.Services.AuthAPI.csproj", "E-commerce.Services.AuthAPI/"]
RUN dotnet restore "E-commerce.Services.AuthAPI/E-commerce.Services.AuthAPI.csproj"

# Copy remaining source files and build
COPY . .
WORKDIR "/src/E-commerce.Services.AuthAPI"
RUN dotnet build "E-commerce.Services.AuthAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "E-commerce.Services.AuthAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "E-commerce.Services.AuthAPI.dll"]
