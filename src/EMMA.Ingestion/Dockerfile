# Base stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS base
WORKDIR /app
# Install krb5 libraries for Npgsql GSSAPI support
RUN apk add --no-cache krb5-libs

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/EMMA.Ingestion/EMMA.Ingestion.csproj", "src/EMMA.Ingestion/"]
COPY ["src/EMMA.ServiceDefaults/EMMA.ServiceDefaults.csproj", "src/EMMA.ServiceDefaults/"]
RUN dotnet restore "src/EMMA.Ingestion/EMMA.Ingestion.csproj"
COPY . .
WORKDIR "/src/src/EMMA.Ingestion"
RUN dotnet build "EMMA.Ingestion.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EMMA.Ingestion.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EMMA.Ingestion.dll"]
