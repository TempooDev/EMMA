# Base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app


# Install krb5 libraries for Npgsql GSSAPI support
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    libgssapi-krb5-2 \
    libkrb5-3 \
    krb5-user \
    && rm -rf /var/lib/apt/lists/* \
    && ln -sf /usr/lib/x86_64-linux-gnu/libgssapi_krb5.so.2 /usr/lib/libgssapi_krb5.so.2

ENV LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu

# Build image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Emma.Identity/Emma.Identity.csproj", "src/Emma.Identity/"]
COPY ["src/EMMA.Shared/EMMA.Shared.csproj", "src/EMMA.Shared/"]
COPY ["src/EMMA.ServiceDefaults/EMMA.ServiceDefaults.csproj", "src/EMMA.ServiceDefaults/"]
RUN dotnet restore "./src/Emma.Identity/Emma.Identity.csproj"
COPY . .
WORKDIR "/src/src/Emma.Identity"
RUN dotnet build "./Emma.Identity.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Emma.Identity.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Emma.Identity.dll"]
