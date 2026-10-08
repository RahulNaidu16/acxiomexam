using System.Threading.RateLimiting;
using AcxiomCRM.Api.Data;
using AcxiomCRM.Api.Models;
using AcxiomCRM.Api.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var railwayPort = Environment.GetEnvironmentVariable("PORT");
if (int.TryParse(railwayPort, out var port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=acxiomcrm.db";

builder.Services.AddDbContext<CrmDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<CrmDbContext>()
.AddDefaultTokenProviders();
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(2));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "AcxiomCRM.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.LoginPath = "/api/auth/login";
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "AcxiomCRM.Csrf";
    options.Cookie.HttpOnly = false;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
var frontendOrigins = builder.Configuration.GetSection("Frontend:Origins").Get<string[]>()
    ?? new[] { "http://localhost:5500", "http://localhost:5501", "http://localhost:5173" };
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins(frontendOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.User);
        if (user is null || !user.IsActive)
        {
            await context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>().SignOutAsync();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }
    await next();
});
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
    {
        await next();
        return;
    }

    try
    {
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
        await next();
    }
    catch (AntiforgeryValidationException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing CSRF token. Fetch /api/auth/csrf and send its token in X-CSRF-TOKEN." });
    }
});
app.UseAuthorization();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    await db.Database.EnsureCreatedAsync();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Manager", "SalesExecutive" })
        if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));

    var email = app.Configuration["BootstrapAdmin:Email"];
    var password = app.Configuration["BootstrapAdmin:Password"];
    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is null)
        {
            var admin = new ApplicationUser { UserName = email, Email = email, FullName = "CRM Administrator", EmailConfirmed = true };
            var result = await users.CreateAsync(admin, password);
            if (result.Succeeded) await users.AddToRoleAsync(admin, "Admin");
            else app.Logger.LogError("Bootstrap admin could not be created: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", application = "AcxiomCRM" }));
app.Run();
