using AcxiomCRM.Api.Data;
using AcxiomCRM.Api.Models;
using AcxiomCRM.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Controllers;

[Route("api/followups"), Authorize]
public sealed class FollowUpsController(CrmDbContext db, UserManager<ApplicationUser> users, IAuditService audit)
    : CrmControllerBase(db, users, audit)
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FollowUpResponse>>> GetAll([FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = Db.FollowUps.AsNoTracking();
        if (IsSalesExecutive) query = query.Where(f => f.AssignedTo == CurrentUserId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(f => f.Status == status);
        if (from is not null) query = query.Where(f => f.FollowUpDate >= from.Value);
        if (to is not null) query = query.Where(f => f.FollowUpDate <= to.Value);
        return Ok((await query.OrderBy(f => f.FollowUpDate).ToListAsync()).Select(ToResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FollowUpResponse>> Get(int id)
    {
        var item = await Visible().AsNoTracking().FirstOrDefaultAsync(f => f.FollowUpId == id);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    public async Task<IActionResult> Create(FollowUpRequest request)
    {
        var invalid = await ValidateRelated(request.CustomerId, request.LeadId);
        if (invalid is not null) return invalid;
        if (request.Status == "Planned" && request.FollowUpDate.Date < DateTime.UtcNow.Date)
            return BadRequest(new { error = "Follow-up date cannot be earlier than today." });
        var owner = await ResolveOwnerAsync(request.AssignedTo);
        if (owner is null) return BadRequest(new { error = "Assigned user does not exist or is inactive." });
        var item = new FollowUp
        {
            CustomerId = request.CustomerId, LeadId = request.LeadId, FollowUpDate = request.FollowUpDate,
            FollowUpType = request.FollowUpType, Remarks = request.Remarks?.Trim(), Status = request.Status, AssignedTo = owner
        };
        Db.FollowUps.Add(item);
        await Db.SaveChangesAsync();
        var response = ToResponse(item);
        await AuditAsync("Create", "FollowUp", item.FollowUpId, after: response);
        return CreatedAtAction(nameof(Get), new { id = item.FollowUpId }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, FollowUpRequest request)
    {
        var item = await Visible().FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (item is null) return NotFound();
        var invalid = await ValidateRelated(request.CustomerId, request.LeadId);
        if (invalid is not null) return invalid;
        if (request.Status == "Planned" && request.FollowUpDate.Date < DateTime.UtcNow.Date)
            return BadRequest(new { error = "Follow-up date cannot be earlier than today." });
        var owner = await ResolveOwnerAsync(request.AssignedTo ?? item.AssignedTo);
        if (owner is null) return BadRequest(new { error = "Assigned user does not exist or is inactive." });
        var before = ToResponse(item);
        item.CustomerId = request.CustomerId; item.LeadId = request.LeadId; item.FollowUpDate = request.FollowUpDate;
        item.FollowUpType = request.FollowUpType; item.Remarks = request.Remarks?.Trim(); item.Status = request.Status; item.AssignedTo = owner;
        await Db.SaveChangesAsync();
        await AuditAsync("Update", "FollowUp", id, before, ToResponse(item));
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] string status)
    {
        if (!new[] { "Planned", "Completed", "Missed", "Cancelled" }.Contains(status)) return BadRequest(new { error = "Invalid follow-up status." });
        var item = await Visible().FirstOrDefaultAsync(f => f.FollowUpId == id);
        if (item is null) return NotFound();
        var old = item.Status;
        item.Status = status;
        await Db.SaveChangesAsync();
        await AuditAsync(status == "Completed" ? "Complete" : "Update", "FollowUp", id, new { status = old }, new { status });
        return NoContent();
    }

    private async Task<IActionResult?> ValidateRelated(int? customerId, int? leadId)
    {
        if ((customerId is null) == (leadId is null)) return BadRequest(new { error = "Specify exactly one related customer or lead." });
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

    private IQueryable<FollowUp> Visible() => IsSalesExecutive ? Db.FollowUps.Where(f => f.AssignedTo == CurrentUserId) : Db.FollowUps;
    private static FollowUpResponse ToResponse(FollowUp f) => new(f.FollowUpId, f.CustomerId, f.LeadId, f.FollowUpDate, f.FollowUpType, f.Remarks, f.Status, f.AssignedTo);
}
