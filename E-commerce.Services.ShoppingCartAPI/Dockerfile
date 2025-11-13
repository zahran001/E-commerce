# Multi-stage build for ShoppingCartAPI
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy both ShoppingCartAPI and MessageBus project files
COPY ["E-commerce.Services.ShoppingCartAPI/E-commerce.Services.ShoppingCartAPI.csproj", "E-commerce.Services.ShoppingCartAPI/"]
COPY ["Ecommerce.MessageBus/Ecommerce.MessageBus.csproj", "Ecommerce.MessageBus/"]
RUN dotnet restore "E-commerce.Services.ShoppingCartAPI/E-commerce.Services.ShoppingCartAPI.csproj"

# Copy remaining source files and build
COPY . .
WORKDIR "/src/E-commerce.Services.ShoppingCartAPI"
RUN dotnet build "E-commerce.Services.ShoppingCartAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "E-commerce.Services.ShoppingCartAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "E-commerce.Services.ShoppingCartAPI.dll"]
