using AcxiomCRM.Api.Data;
using AcxiomCRM.Api.Models;
using AcxiomCRM.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Controllers;

[Route("api/leads"), Authorize]
public sealed class LeadsController(CrmDbContext db, UserManager<ApplicationUser> users, IAuditService audit)
    : CrmControllerBase(db, users, audit)
{
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        ["New"] = ["Contacted", "Unqualified", "Lost"],
        ["Contacted"] = ["Qualified", "Unqualified", "Lost"],
        ["Qualified"] = ["Contacted", "Converted", "Lost"],
        ["Unqualified"] = ["Contacted", "Lost"], ["Converted"] = [], ["Lost"] = []
    };

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeadResponse>>> GetAll([FromQuery] string? search, [FromQuery] string? status)
    {
        var query = Db.Leads.AsNoTracking();
        if (IsSalesExecutive) query = query.Where(l => l.AssignedTo == CurrentUserId);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(l => l.LeadName.Contains(search) || (l.CompanyName ?? "").Contains(search));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(l => l.Status == status);
        return Ok((await query.OrderBy(l => l.LeadName).ToListAsync()).Select(ToResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeadResponse>> Get(int id)
    {
        var lead = await VisibleLeads().AsNoTracking().FirstOrDefaultAsync(l => l.LeadId == id);
        return lead is null ? NotFound() : Ok(ToResponse(lead));
    }

    [HttpPost]
    public async Task<ActionResult<LeadResponse>> Create(LeadRequest request)
    {
        if (request.Status != "New") return BadRequest(new { error = "New leads must start with status New." });
        var owner = await ResolveOwnerAsync(request.AssignedTo);
        if (owner is null) return BadRequest(new { error = "Assigned user does not exist or is inactive." });
        var lead = new Lead
        {
            LeadCode = $"LED-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), LeadName = request.LeadName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(), Phone = request.Phone.Trim(),
            CompanyName = request.CompanyName?.Trim(), Source = request.Source, Status = request.Status,
            ExpectedValue = request.ExpectedValue, AssignedTo = owner, CreatedDate = DateTime.UtcNow
        };
        Db.Leads.Add(lead);
        await Db.SaveChangesAsync();
        var response = ToResponse(lead);
        await AuditAsync("Create", "Lead", lead.LeadId, after: response);
        return CreatedAtAction(nameof(Get), new { id = lead.LeadId }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, LeadRequest request)
    {
        var lead = await VisibleLeads().FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead is null) return NotFound();
        if (request.Status != lead.Status && !Transitions[lead.Status].Contains(request.Status))
            return BadRequest(new { error = $"Cannot move lead from {lead.Status} to {request.Status}." });
        if (request.Status == "Converted" && lead.Status != "Converted")
            return BadRequest(new { error = "Use POST /api/leads/{id}/convert to convert a qualified lead." });
        if (lead.Status == "Converted" && request.Status != "Converted")
            return BadRequest(new { error = "Converted leads cannot be reopened." });
        var owner = await ResolveOwnerAsync(request.AssignedTo ?? lead.AssignedTo);
        if (owner is null) return BadRequest(new { error = "Assigned user does not exist or is inactive." });
        var before = ToResponse(lead);
        lead.LeadName = request.LeadName.Trim(); lead.Email = request.Email.Trim().ToLowerInvariant();
        lead.Phone = request.Phone.Trim(); lead.CompanyName = request.CompanyName?.Trim();
        lead.Source = request.Source; lead.Status = request.Status; lead.ExpectedValue = request.ExpectedValue; lead.AssignedTo = owner;
        await Db.SaveChangesAsync();
        await AuditAsync("Update", "Lead", id, before, ToResponse(lead));
        return NoContent();
    }

    [HttpPost("{id:int}/convert")]
    public async Task<IActionResult> Convert(int id, ConvertLeadRequest? request)
    {
        var lead = await VisibleLeads().FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead is null) return NotFound();
        if (lead.Status != "Qualified") return BadRequest(new { error = "Only qualified leads can be converted." });
        var amount = request?.Amount is > 0 ? request.Amount.Value : Math.Max(lead.ExpectedValue, 1m);
        var customer = await Db.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == lead.Email.ToLower() || c.Phone == lead.Phone);
        if (customer is not null && (customer.Status != "Active" || (IsSalesExecutive && customer.AssignedTo != CurrentUserId)))
            return Conflict(new { error = "The matching customer is inactive or outside your assigned scope." });
        await using var transaction = await Db.Database.BeginTransactionAsync();
        if (customer is null)
        {
            customer = new Customer
            {
                CustomerCode = $"CUS-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), CustomerName = lead.LeadName,
                Email = lead.Email, Phone = lead.Phone, CompanyName = lead.CompanyName, Status = "Active",
                AssignedTo = lead.AssignedTo, CreatedBy = CurrentUserId, CreatedDate = DateTime.UtcNow
            };
            Db.Customers.Add(customer);
            await Db.SaveChangesAsync();
            await AuditAsync("Create", "Customer", customer.CustomerId, after: new { customer.CustomerName, customer.Email, customer.Phone });
        }
        var opportunity = new Opportunity
        {
            OpportunityName = lead.LeadName + " - Opportunity", CustomerId = customer.CustomerId, LeadId = lead.LeadId,
            Amount = amount, Stage = "Qualification", Probability = 20, ExpectedCloseDate = DateTime.UtcNow.Date.AddDays(30),
            Status = "Open", AssignedTo = lead.AssignedTo, CreatedDate = DateTime.UtcNow
        };
        var oldStatus = lead.Status;
        lead.Status = "Converted";
        Db.Opportunities.Add(opportunity);
        await Db.SaveChangesAsync();
        await AuditAsync("Convert", "Lead", id, new { status = oldStatus }, new { status = lead.Status, customerId = customer.CustomerId, opportunityId = opportunity.OpportunityId });
        await transaction.CommitAsync();
        return Ok(new { customerId = customer.CustomerId, opportunity = new OpportunityResponse(opportunity.OpportunityId,
            opportunity.OpportunityName, opportunity.CustomerId, opportunity.LeadId, opportunity.Amount, opportunity.Stage,
            opportunity.Probability, opportunity.ExpectedCloseDate, opportunity.Status, opportunity.CreatedDate,
            opportunity.AssignedTo, opportunity.WeightedAmount) });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (IsSalesExecutive) return Forbid();
        var lead = await Db.Leads.FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead is null) return NotFound();
        var before = ToResponse(lead);
        Db.Leads.Remove(lead);
        await Db.SaveChangesAsync();
        await AuditAsync("Delete", "Lead", id, before);
        return NoContent();
    }

    private IQueryable<Lead> VisibleLeads()
        => IsSalesExecutive ? Db.Leads.Where(l => l.AssignedTo == CurrentUserId) : Db.Leads;

    private static LeadResponse ToResponse(Lead l)
        => new(l.LeadId, l.LeadCode, l.LeadName, l.Email, l.Phone, l.CompanyName, l.Source, l.Status,
            l.ExpectedValue, l.CreatedDate, l.AssignedTo);
}
