# Base stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
# Install krb5 libraries for Npgsql GSSAPI support
RUN apk add --no-cache krb5-libs
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/EMMA.Server/EMMA.Server.csproj", "src/EMMA.Server/"]
COPY ["src/EMMA.ServiceDefaults/EMMA.ServiceDefaults.csproj", "src/EMMA.ServiceDefaults/"]
RUN dotnet restore "src/EMMA.Server/EMMA.Server.csproj"
COPY . .
WORKDIR "/src/src/EMMA.Server"
RUN dotnet build "EMMA.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EMMA.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EMMA.Server.dll"]
