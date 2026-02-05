# Base stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS base
WORKDIR /app

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/EMMA.MarketService/EMMA.MarketService.csproj", "src/EMMA.MarketService/"]
COPY ["src/EMMA.ServiceDefaults/EMMA.ServiceDefaults.csproj", "src/EMMA.ServiceDefaults/"]
RUN dotnet restore "src/EMMA.MarketService/EMMA.MarketService.csproj"
COPY . .
WORKDIR "/src/src/EMMA.MarketService"
RUN dotnet build "EMMA.MarketService.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EMMA.MarketService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EMMA.MarketService.dll"]
