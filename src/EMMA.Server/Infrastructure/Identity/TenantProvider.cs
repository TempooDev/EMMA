using System.Security.Claims;

namespace EMMA.Server.Infrastructure.Identity;

public interface ITenantProvider
{
    string? TenantId { get; }
}

public class TenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public string? TenantId => httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value;
}
