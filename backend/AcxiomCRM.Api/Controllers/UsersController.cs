using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Api.Models;
using AcxiomCRM.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Controllers;

public sealed class CreateUserRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";
    [Required, RegularExpression("^(Admin|Manager|SalesExecutive)$")] public string Role { get; set; } = "SalesExecutive";
}
public sealed class UpdateUserRequest
{
    [Required, RegularExpression("^(Admin|Manager|SalesExecutive)$")] public string Role { get; set; } = "SalesExecutive";
    public bool IsActive { get; set; }
}

[ApiController, Route("api/users"), Authorize(Roles = "Admin")]
public sealed class UsersController(UserManager<ApplicationUser> users, IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? search)
    {
        var records = await users.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();
        var response = new List<object>();
        foreach (var user in records)
        {
            if (!string.IsNullOrWhiteSpace(search) && !user.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !(user.Email ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            response.Add(await Shape(user));
        }
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), FullName = request.FullName.Trim(), EmailConfirmed = true };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        await users.AddToRoleAsync(user, request.Role);
        await audit.WriteAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            "Create", "User", user.Id, newValue: new { user.Email, user.FullName, role = request.Role, user.IsActive },
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        return Created($"/api/users/{user.Id}", await Shape(user));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateUserRequest request)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        var actorId = users.GetUserId(User);
        if (user.Id == actorId && (!request.IsActive || request.Role != "Admin"))
            return BadRequest(new { error = "You cannot deactivate yourself or remove your own Admin role." });
        var oldRoles = await users.GetRolesAsync(user);
        var oldActive = user.IsActive;
        var remove = await users.RemoveFromRolesAsync(user, oldRoles);
        if (!remove.Succeeded) return BadRequest(new { errors = remove.Errors.Select(e => e.Description) });
        var add = await users.AddToRoleAsync(user, request.Role);
        if (!add.Succeeded)
        {
            foreach (var role in oldRoles) await users.AddToRoleAsync(user, role);
            return BadRequest(new { errors = add.Errors.Select(e => e.Description) });
        }
        user.IsActive = request.IsActive;
        if (!request.IsActive) await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        else if (user.LockoutEnd > DateTimeOffset.UtcNow) await users.SetLockoutEndDateAsync(user, null);
        await users.UpdateAsync(user);
        await users.UpdateSecurityStampAsync(user);
        await audit.WriteAsync(actorId, "Update", "User", user.Id,
            newValue: new { oldRole = oldRoles.FirstOrDefault(), newRole = request.Role, oldActive, newActive = user.IsActive },
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(await Shape(user));
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        await audit.WriteAsync(users.GetUserId(User), "Unlock", "User", id,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        return NoContent();
    }

    private async Task<object> Shape(ApplicationUser user)
        => new { user.Id, user.FullName, user.Email, user.IsActive, roles = await users.GetRolesAsync(user),
            lockedUntilUtc = user.LockoutEnd, failedAccessCount = user.AccessFailedCount };
}
