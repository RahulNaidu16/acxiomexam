using System.Text.Json;
using AcxiomCRM.Api.Data;
using AcxiomCRM.Api.Models;

namespace AcxiomCRM.Api.Services;

public interface IAuditService
{
    Task WriteAsync(string? userId, string action, string entity, object? recordId = null,
        object? oldValue = null, object? newValue = null, string? ipAddress = null);
}

public sealed class AuditService(CrmDbContext db) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(string? userId, string action, string entity, object? recordId = null,
        object? oldValue = null, object? newValue = null, string? ipAddress = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entity,
            RecordId = recordId?.ToString(),
            OldValue = oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
            IpAddress = ipAddress,
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
