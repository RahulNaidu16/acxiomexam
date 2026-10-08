using AcxiomCRM.Api.Data;
using AcxiomCRM.Api.Models;
using AcxiomCRM.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Controllers;

[ApiController]
public abstract class CrmControllerBase(CrmDbContext db, UserManager<ApplicationUser> users, IAuditService audit) : ControllerBase
{
    protected readonly CrmDbContext Db = db;
    protected readonly UserManager<ApplicationUser> Users = users;
    protected readonly IAuditService Audit = audit;
    protected string CurrentUserId => Users.GetUserId(User)!;
    protected bool IsSalesExecutive => User.IsInRole("SalesExecutive");
    protected bool CanManageAll => User.IsInRole("Admin") || User.IsInRole("Manager");

    protected async Task<string?> ResolveOwnerAsync(string? proposed)
    {
        if (IsSalesExecutive) return CurrentUserId;
        var ownerId = string.IsNullOrWhiteSpace(proposed) ? CurrentUserId : proposed;
        var owner = await Users.FindByIdAsync(ownerId);
        return owner is { IsActive: true } ? owner.Id : null;
    }

    protected Task AuditAsync(string action, string entity, object? id, object? before = null, object? after = null)
        => Audit.WriteAsync(CurrentUserId, action, entity, id, before, after, HttpContext.Connection.RemoteIpAddress?.ToString());
}
