using AcxiomCRM.Api.Data;
using AcxiomCRM.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Controllers;

[ApiController, Route("api/dashboard"), Authorize]
public sealed class DashboardController(CrmDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = users.GetUserId(User)!;
        var customers = db.Customers.AsNoTracking();
        var leads = db.Leads.AsNoTracking();
        var opportunities = db.Opportunities.AsNoTracking();
        var followups = db.FollowUps.AsNoTracking();
        if (User.IsInRole("SalesExecutive"))
        {
            customers = customers.Where(x => x.AssignedTo == userId);
            leads = leads.Where(x => x.AssignedTo == userId);
            opportunities = opportunities.Where(x => x.AssignedTo == userId);
            followups = followups.Where(x => x.AssignedTo == userId);
        }
        var c = await customers.CountAsync();
        var leadRows = await leads.ToListAsync();
        var oppRows = await opportunities.ToListAsync();
        var openOpps = oppRows.Where(o => o.Status == "Open").ToArray();
        var leadStatuses = new[] { "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost" }
            .Select(s => new { status = s, count = leadRows.Count(l => l.Status == s) });
        var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" }
            .Select(s => new { stage = s, count = oppRows.Count(o => o.Stage == s), amount = oppRows.Where(o => o.Stage == s).Sum(o => o.Amount) });
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-5);
        var monthlySales = oppRows.Where(o => o.Stage == "Won" && o.ExpectedCloseDate >= monthStart && o.ExpectedCloseDate.Date <= DateTime.UtcNow.Date)
            .GroupBy(o => new { o.ExpectedCloseDate.Year, o.ExpectedCloseDate.Month })
            .Select(g => new { month = $"{g.Key.Year:D4}-{g.Key.Month:D2}", amount = g.Sum(o => o.Amount) }).ToArray();
        var result = new DashboardResponse(c, leadRows.Count,
            leadRows.Count(l => l.Status is not ("Converted" or "Lost" or "Unqualified")),
            oppRows.Count, openOpps.Length, oppRows.Count(o => o.Status == "Won"), oppRows.Count(o => o.Status == "Lost"),
            openOpps.Sum(o => o.Amount), openOpps.Sum(o => o.WeightedAmount),
            await followups.CountAsync(f => f.Status == "Planned"));
        return Ok(new { kpis = result, leadStatuses, opportunityStages = stages, monthlySales });
    }
}

[ApiController, Route("api/reports/pipeline"), Authorize]
public sealed class PipelineReportController(CrmDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && !User.IsInRole("SalesExecutive")) return Forbid();
        var query = db.Opportunities.AsNoTracking();
        if (User.IsInRole("SalesExecutive"))
        {
            var id = users.GetUserId(User)!;
            query = query.Where(o => o.AssignedTo == id);
        }
        var opportunities = await query.ToListAsync();
        var data = opportunities.GroupBy(o => new { o.Stage, o.AssignedTo })
            .Select(g => new { g.Key.Stage, assignedTo = g.Key.AssignedTo, count = g.Count(), amount = g.Sum(o => o.Amount), weightedAmount = g.Sum(o => o.WeightedAmount) })
            .ToArray();
        return Ok(data);
    }
}
