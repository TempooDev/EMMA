using System.Security.Claims;

namespace EMMA.Api.Infrastructure.Identity;

public interface ITenantProvider
{
    string? TenantId { get; }
    string? UserId { get; }
    bool IsSandbox { get; }
}

public class TenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public string? TenantId => httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
    public string? UserId => httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    public bool IsSandbox => httpContextAccessor.HttpContext?.Request.Headers.ContainsKey("X-Sandbox") ?? false;
}
