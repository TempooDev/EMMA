# EMMA Authentication Guide

This guide explains how to authenticate with the EMMA platform, specifically for the **Developer API** (`EMMA.Api`).

## 1. Obtain a JWT Token

Authentication in EMMA is primarily done via JWT (JSON Web Tokens). You can obtain a token by using the `admin` credentials against the Identity service.

### Endpoint
`POST /connect/token` (on the `emma-identity` service)

### Example Request (curl)
```bash
curl -X POST http://localhost:<IDENTITY_PORT>/connect/token \
     -H "Content-Type: application/x-www-form-urlencoded" \
     -d "username=admin" \
     -d "password=Admin123!"
```

### Response
```json
{
  "access_token": "eyJhbG...",
  "token_type": "Bearer",
  "expires_in": 7200
}
```

## 2. Generate an API Key (Optional)

Once you have a JWT, you can generate a persistent API Key for your tenant.

### Endpoint
`POST /api/keys` (on the `emma-identity` service)

### Example Request (curl)
```bash
curl -X POST http://localhost:<IDENTITY_PORT>/api/keys \
     -H "Authorization: Bearer <YOUR_JWT_TOKEN>"
```

### Response
```json
{
  "ApiKey": "550e8400e29b41d4a716446655440000"
}
```

## 3. Using Authentication with EMMA API

When calling endpoints on the `emma-api` service, you can use either the JWT or the API Key.

### Option A: Using JWT
Add the token to the `Authorization` header:
- Header: `Authorization`
- Value: `Bearer <YOUR_JWT_TOKEN>`

### Option B: Using API Key
Add the key to the `X-API-KEY` header:
- Header: `X-API-KEY`
- Value: `<YOUR_API_KEY>`

---

> [!NOTE]
> The ports for `emma-identity` and `emma-api` are dynamic when running with .NET Aspire. Please check the **Aspire Dashboard** to find the correct local URLs.

> [!IMPORTANT]
> The internal dashboard (frontend) uses `EMMA.Server`, which currently does not require authentication for its basic telemetry views. If the dashboard is not showing data, verify that the `emma-db` is running and has telemetry data.
