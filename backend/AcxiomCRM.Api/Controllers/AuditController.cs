using AcxiomCRM.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Controllers;

[ApiController, Route("api/audit"), Authorize(Roles = "Admin")]
public sealed class AuditController(CrmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? userId, [FromQuery] string? module,
        [FromQuery] string? action, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(x => x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(module)) query = query.Where(x => x.EntityName.Contains(module));
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action.Contains(action));
        if (from is not null) query = query.Where(x => x.CreatedDate >= from.Value);
        if (to is not null) query = query.Where(x => x.CreatedDate <= to.Value);
        var count = await query.CountAsync();
        var records = await query.OrderByDescending(x => x.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.AuditLogId, x.UserId, x.Action, x.EntityName, x.RecordId, x.OldValue, x.NewValue, x.CreatedDate, x.IpAddress })
            .ToListAsync();
        return Ok(new { page, pageSize, total = count, items = records });
    }
}
