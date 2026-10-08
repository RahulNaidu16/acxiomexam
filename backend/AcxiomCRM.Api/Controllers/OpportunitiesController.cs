using AcxiomCRM.Api.Data;
using AcxiomCRM.Api.Models;
using AcxiomCRM.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Controllers;

[Route("api/opportunities"), Authorize]
public sealed class OpportunitiesController(CrmDbContext db, UserManager<ApplicationUser> users, IAuditService audit)
    : CrmControllerBase(db, users, audit)
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OpportunityResponse>>> GetAll([FromQuery] string? search, [FromQuery] string? stage)
    {
        var query = Db.Opportunities.AsNoTracking();
        if (IsSalesExecutive) query = query.Where(o => o.AssignedTo == CurrentUserId);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(o => o.OpportunityName.Contains(search));
        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(o => o.Stage == stage);
        return Ok((await query.OrderBy(o => o.ExpectedCloseDate).ToListAsync()).Select(ToResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OpportunityResponse>> Get(int id)
    {
        var item = await VisibleOpportunities().AsNoTracking().FirstOrDefaultAsync(o => o.OpportunityId == id);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    public async Task<IActionResult> Create(OpportunityRequest request)
    {
        var invalid = await ValidateBusinessRules(request);
        if (invalid is not null) return invalid;
        var owner = await ResolveOwnerAsync(request.AssignedTo);
        if (owner is null) return BadRequest(new { error = "Assigned user does not exist or is inactive." });
        var item = new Opportunity
        {
            OpportunityName = request.OpportunityName.Trim(), CustomerId = request.CustomerId, LeadId = request.LeadId,
            Amount = request.Amount, Stage = request.Stage, Probability = request.Probability,
            ExpectedCloseDate = request.ExpectedCloseDate, Status = request.Stage is "Won" or "Lost" ? request.Stage : "Open",
            AssignedTo = owner, CreatedDate = DateTime.UtcNow
        };
        Db.Opportunities.Add(item);
        await Db.SaveChangesAsync();
        var response = ToResponse(item);
        await AuditAsync("Create", "Opportunity", item.OpportunityId, after: response);
        return CreatedAtAction(nameof(Get), new { id = item.OpportunityId }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, OpportunityRequest request)
    {
        var item = await VisibleOpportunities().FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (item is null) return NotFound();
        var invalid = await ValidateBusinessRules(request);
        if (invalid is not null) return invalid;
        var owner = await ResolveOwnerAsync(request.AssignedTo ?? item.AssignedTo);
        if (owner is null) return BadRequest(new { error = "Assigned user does not exist or is inactive." });
        var before = ToResponse(item);
        item.OpportunityName = request.OpportunityName.Trim(); item.CustomerId = request.CustomerId; item.LeadId = request.LeadId;
        item.Amount = request.Amount; item.Stage = request.Stage; item.Probability = request.Probability;
        item.ExpectedCloseDate = request.ExpectedCloseDate; item.Status = request.Stage is "Won" or "Lost" ? request.Stage : "Open";
        item.AssignedTo = owner;
        await Db.SaveChangesAsync();
        await AuditAsync("Update", "Opportunity", id, before, ToResponse(item));
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (IsSalesExecutive) return Forbid();
        var item = await Db.Opportunities.FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (item is null) return NotFound();
        var before = ToResponse(item);
        Db.Opportunities.Remove(item);
        await Db.SaveChangesAsync();
        await AuditAsync("Delete", "Opportunity", id, before);
        return NoContent();
    }

    private async Task<IActionResult?> ValidateBusinessRules(OpportunityRequest request)
    {
        if (request.Probability is < 0 or > 100) return BadRequest(new { error = "Probability must be between 0 and 100." });
        if (!new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" }.Contains(request.Stage))
            return BadRequest(new { error = "Invalid opportunity stage." });
        if (request.Amount < 0 || (request.Stage is not ("Won" or "Lost") && request.Amount <= 0))
            return BadRequest(new { error = "Opportunity Amount must be greater than 0 for an active opportunity." });
        if (request.Stage is not ("Won" or "Lost") && request.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            return BadRequest(new { error = "Expected Close Date cannot be in the past." });
        if (request.CustomerId is not null)
        {
            var customerQuery = Db.Customers.Where(c => c.CustomerId == request.CustomerId && c.Status == "Active");
            if (IsSalesExecutive) customerQuery = customerQuery.Where(c => c.AssignedTo == CurrentUserId);
            if (!await customerQuery.AnyAsync()) return BadRequest(new { error = "Customer does not exist or is not accessible." });
        }
        if (request.LeadId is not null)
        {
            var leadQuery = Db.Leads.Where(l => l.LeadId == request.LeadId);
            if (IsSalesExecutive) leadQuery = leadQuery.Where(l => l.AssignedTo == CurrentUserId);
            if (!await leadQuery.AnyAsync()) return BadRequest(new { error = "Lead does not exist or is not accessible." });
        }
        return null;
    }

    private IQueryable<Opportunity> VisibleOpportunities()
        => IsSalesExecutive ? Db.Opportunities.Where(o => o.AssignedTo == CurrentUserId) : Db.Opportunities;

    private static OpportunityResponse ToResponse(Opportunity o)
        => new(o.OpportunityId, o.OpportunityName, o.CustomerId, o.LeadId, o.Amount, o.Stage,
            o.Probability, o.ExpectedCloseDate, o.Status, o.CreatedDate, o.AssignedTo, o.WeightedAmount);
}
