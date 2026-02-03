using System.Security.Claims;

namespace EMMA.Api.Infrastructure.Identity;

public interface ITenantProvider
{
    string? GetTenantId();
}

public class TenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    public string? GetTenantId()
    {
        return httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");
    }
}
