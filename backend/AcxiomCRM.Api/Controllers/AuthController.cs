using AcxiomCRM.Api.Models;
using AcxiomCRM.Api.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IAntiforgery antiforgery,
    IAuditService audit) : ControllerBase
{
    [HttpGet("csrf"), AllowAnonymous]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    [HttpPost("register"), AllowAnonymous, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(), Email = request.Email.Trim(),
            FullName = request.FullName.Trim(), EmailConfirmed = true
        };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        await users.AddToRoleAsync(user, "SalesExecutive");
        await audit.WriteAsync(user.Id, "Register", "Auth", user.Id,
            newValue: new { user.Email, user.FullName }, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        await signIn.SignInAsync(user, isPersistent: false);
        return Created("/api/auth/me", new UserResponse(user.Id, user.FullName, user.Email!, ["SalesExecutive"]));
    }

    [HttpPost("login"), AllowAnonymous, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim();
        var user = await users.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            await audit.WriteAsync(user?.Id, "LoginFailed", "Auth", user?.Id,
                newValue: new { email, reason = "invalid-or-inactive" }, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
            return Unauthorized(new { error = "Invalid credentials or inactive account." });
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await audit.WriteAsync(user.Id, result.IsLockedOut ? "Lockout" : "LoginFailed", "Auth", user.Id,
                newValue: new { reason = result.IsLockedOut ? "locked" : "invalid-credentials" },
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
            return Unauthorized(new { error = result.IsLockedOut ? "Account locked. Try again in five minutes." : "Invalid credentials." });
        }

        await audit.WriteAsync(user.Id, "Login", "Auth", user.Id,
            newValue: new { user.Email }, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(await UserResponseAsync(user));
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = users.GetUserId(User);
        await signIn.SignOutAsync();
        await audit.WriteAsync(userId, "Logout", "Auth", userId, ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(await UserResponseAsync(user));
    }

    private async Task<UserResponse> UserResponseAsync(ApplicationUser user)
        => new(user.Id, user.FullName, user.Email ?? "", (await users.GetRolesAsync(user)).ToArray());
}
