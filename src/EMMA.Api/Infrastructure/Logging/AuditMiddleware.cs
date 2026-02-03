using System.Text.Json;
using EMMA.Api.Infrastructure.Identity;
using Npgsql;
using System.Security.Claims;
using Dapper;

namespace EMMA.Api.Infrastructure.Logging;

public class AuditMiddleware(RequestDelegate next, NpgsqlDataSource dataSource)
{
    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        var request = context.Request;
        
        // We only audit write/control operations by default, or specific sensitive paths
        var isWriteAction = request.Method == HttpMethods.Post || 
                            request.Method == HttpMethods.Put || 
                            request.Method == HttpMethods.Delete;

        if (!isWriteAction && !request.Path.StartsWithSegments("/connect/token"))
        {
            await next(context);
            return;
        }

        // Capture request body for auditing
        string? payload = null;
        if (isWriteAction)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            payload = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Proceed with the request
        await next(context);

        // After request: Log the outcome
        try
        {
            var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier) 
                         ?? context.User?.FindFirstValue("sub") 
                         ?? "anonymous";
            var tenantId = tenantProvider.TenantId ?? "N/A";

            using var connection = await dataSource.OpenConnectionAsync();
            const string sql = @"
                INSERT INTO audit_logs (id, timestamp, user_id, tenant_id, action, path, method, payload, status_code)
                VALUES (gen_random_uuid(), NOW(), @UserId, @TenantId, @Action, @Path, @Method, @Payload::jsonb, @StatusCode)";

            await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                TenantId = tenantId,
                Action = $"{request.Method} {request.Path}",
                Path = request.Path.ToString(),
                Method = request.Method,
                Payload = string.IsNullOrEmpty(payload) ? null : payload,
                StatusCode = context.Response.StatusCode
            });
        }
        catch (Exception ex)
        {
            // Fail silent for audit logging to not block business logic, 
            // but log the failure to standard logs.
            Console.WriteLine($"Audit Logging Failed: {ex.Message}");
        }
    }
}
