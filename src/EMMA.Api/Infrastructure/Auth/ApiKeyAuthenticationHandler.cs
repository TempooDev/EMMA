using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Npgsql;
using Dapper;

namespace EMMA.Api.Infrastructure.Auth;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string Scheme => DefaultScheme;
    public string AuthenticationType => DefaultScheme;
}

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    NpgsqlDataSource dataSource)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    private const string ApiKeyHeaderName = "X-API-KEY";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        using var connection = await dataSource.OpenConnectionAsync();
        var apiKeyInfo = await connection.QuerySingleOrDefaultAsync<ApiKeyInfo>(
            "SELECT owner_id as OwnerId, tenant_id as TenantId, is_active as IsActive FROM api_keys WHERE key = @Key",
            new { Key = providedApiKey });

        if (apiKeyInfo == null || !apiKeyInfo.IsActive)
        {
            return AuthenticateResult.Fail("Invalid API Key");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, apiKeyInfo.OwnerId),
            new("tenant_id", apiKeyInfo.TenantId),
            new("auth_method", "api_key")
        };

        var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Options.Scheme);

        return AuthenticateResult.Success(ticket);
    }

    private class ApiKeyInfo
    {
        public string OwnerId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
