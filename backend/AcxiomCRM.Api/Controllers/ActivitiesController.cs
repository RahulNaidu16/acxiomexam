using AcxiomCRM.Api.Data;
using AcxiomCRM.Api.Models;
using AcxiomCRM.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Controllers;

[Route("api/activities"), Authorize]
public sealed class ActivitiesController(CrmDbContext db, UserManager<ApplicationUser> users, IAuditService audit)
    : CrmControllerBase(db, users, audit)
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivityResponse>>> GetAll([FromQuery] string? type, [FromQuery] string? status)
    {
        var query = Db.Activities.AsNoTracking();
        if (IsSalesExecutive) query = query.Where(a => a.AssignedTo == CurrentUserId);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(a => a.ActivityType == type);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        return Ok((await query.OrderBy(a => a.ActivityDate).ToListAsync()).Select(ToResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivityResponse>> Get(int id)
    {
        var query = Db.Activities.AsNoTracking().Where(a => a.ActivityId == id);
        if (IsSalesExecutive) query = query.Where(a => a.AssignedTo == CurrentUserId);
        var item = await query.FirstOrDefaultAsync();
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    public async Task<IActionResult> Create(ActivityRequest request)
    {
        var invalid = await ValidateRelated(request.CustomerId, request.LeadId);
        if (invalid is not null) return invalid;
        var owner = await ResolveOwnerAsync(request.AssignedTo);
        if (owner is null) return BadRequest(new { error = "Assigned user does not exist or is inactive." });
        var item = new Activity
        {
            ActivityType = request.ActivityType, Subject = request.Subject.Trim(), Description = request.Description?.Trim(),
            ActivityDate = request.ActivityDate, CustomerId = request.CustomerId, LeadId = request.LeadId,
            AssignedTo = owner, Status = request.Status
        };
        Db.Activities.Add(item);
        await Db.SaveChangesAsync();
        var response = ToResponse(item);
        await AuditAsync("Create", "Activity", item.ActivityId, after: response);
        return Created($"/api/activities/{item.ActivityId}", response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ActivityRequest request)
    {
        var query = Db.Activities.Where(a => a.ActivityId == id);
        if (IsSalesExecutive) query = query.Where(a => a.AssignedTo == CurrentUserId);
        var item = await query.FirstOrDefaultAsync();
        if (item is null) return NotFound();
        var invalid = await ValidateRelated(request.CustomerId, request.LeadId);
        if (invalid is not null) return invalid;
        var owner = await ResolveOwnerAsync(request.AssignedTo ?? item.AssignedTo);
        if (owner is null) return BadRequest(new { error = "Assigned user does not exist or is inactive." });
        var before = ToResponse(item);
        item.ActivityType = request.ActivityType; item.Subject = request.Subject.Trim();
        item.Description = request.Description?.Trim(); item.ActivityDate = request.ActivityDate;
        item.CustomerId = request.CustomerId; item.LeadId = request.LeadId;
        item.Status = request.Status; item.AssignedTo = owner;
        await Db.SaveChangesAsync();
        await AuditAsync("Update", "Activity", id, before, ToResponse(item));
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (IsSalesExecutive) return Forbid();
        var item = await Db.Activities.FirstOrDefaultAsync(a => a.ActivityId == id);
        if (item is null) return NotFound();
        var before = ToResponse(item);
        Db.Activities.Remove(item);
        await Db.SaveChangesAsync();
        await AuditAsync("Delete", "Activity", id, before);
        return NoContent();
    }

    private async Task<IActionResult?> ValidateRelated(int? customerId, int? leadId)
    {
        if (customerId is not null && leadId is not null) return BadRequest(new { error = "Specify at most one related customer or lead." });
        if (customerId is not null)
        {
            var q = Db.Customers.Where(c => c.CustomerId == customerId);
            if (IsSalesExecutive) q = q.Where(c => c.AssignedTo == CurrentUserId);
            if (!await q.AnyAsync()) return BadRequest(new { error = "Customer does not exist or is not accessible." });
        }
        if (leadId is not null)
        {
            var q = Db.Leads.Where(l => l.LeadId == leadId);
            if (IsSalesExecutive) q = q.Where(l => l.AssignedTo == CurrentUserId);
            if (!await q.AnyAsync()) return BadRequest(new { error = "Lead does not exist or is not accessible." });
        }
        return null;
    }

    private static ActivityResponse ToResponse(Activity a)
        => new(a.ActivityId, a.ActivityType, a.Subject, a.Description, a.ActivityDate, a.CustomerId, a.LeadId, a.AssignedTo, a.Status);
}
