using Microsoft.AspNetCore.Identity;

namespace Emma.Identity.Models;

public class ApplicationUser : IdentityUser
{
    public string? TenantId { get; set; }
    public string? AssignedAssets { get; set; } // Comma-separated or JSON
}
