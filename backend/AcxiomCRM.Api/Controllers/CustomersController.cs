using AcxiomCRM.Api.Data;
using AcxiomCRM.Api.Models;
using AcxiomCRM.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Controllers;

[Route("api/customers"), Authorize]
public sealed class CustomersController(CrmDbContext db, UserManager<ApplicationUser> users, IAuditService audit)
    : CrmControllerBase(db, users, audit)
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerResponse>>> GetAll([FromQuery] string? search, [FromQuery] string? status)
    {
        var query = Db.Customers.AsNoTracking();
        if (IsSalesExecutive) query = query.Where(c => c.AssignedTo == CurrentUserId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.CustomerName.Contains(search) || c.Email.Contains(search) || c.Phone.Contains(search) || (c.CompanyName ?? "").Contains(search));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);
        var records = await query.OrderBy(c => c.CustomerName).ToListAsync();
        return Ok(records.Select(ToResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerResponse>> Get(int id)
    {
        var customer = await VisibleCustomers().AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id);
        return customer is null ? NotFound() : Ok(ToResponse(customer));
    }

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(CustomerRequest request)
    {
        if (await Db.Customers.AnyAsync(c => c.Email.ToLower() == request.Email.ToLower() || c.Phone == request.Phone))
            return Conflict(new { error = "A customer with this email or phone already exists." });
        var owner = await ResolveOwnerAsync(request.AssignedTo);
        if (owner is null) return BadRequest(new { error = "Assigned user does not exist or is inactive." });
        var customer = new Customer
        {
            CustomerCode = $"CUS-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            CustomerName = request.CustomerName.Trim(), Email = request.Email.Trim().ToLowerInvariant(),
            Phone = request.Phone.Trim(), CompanyName = request.CompanyName?.Trim(), Address = request.Address?.Trim(),
            City = request.City?.Trim(), State = request.State?.Trim(), Status = request.Status,
            AssignedTo = owner, CreatedBy = CurrentUserId, CreatedDate = DateTime.UtcNow
        };
        Db.Customers.Add(customer);
        await Db.SaveChangesAsync();
        var response = ToResponse(customer);
        await AuditAsync("Create", "Customer", customer.CustomerId, after: response);
        return CreatedAtAction(nameof(Get), new { id = customer.CustomerId }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CustomerRequest request)
    {
        var customer = await VisibleCustomers().FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer is null) return NotFound();
        if (await Db.Customers.AnyAsync(c => c.CustomerId != id && (c.Email.ToLower() == request.Email.ToLower() || c.Phone == request.Phone)))
            return Conflict(new { error = "A customer with this email or phone already exists." });
        var owner = await ResolveOwnerAsync(request.AssignedTo ?? customer.AssignedTo);
        if (owner is null) return BadRequest(new { error = "Assigned user does not exist or is inactive." });
        var before = ToResponse(customer);
        customer.CustomerName = request.CustomerName.Trim(); customer.Email = request.Email.Trim().ToLowerInvariant();
        customer.Phone = request.Phone.Trim(); customer.CompanyName = request.CompanyName?.Trim();
        customer.Address = request.Address?.Trim(); customer.City = request.City?.Trim(); customer.State = request.State?.Trim();
        customer.Status = request.Status; customer.AssignedTo = owner;
        await Db.SaveChangesAsync();
        await AuditAsync("Update", "Customer", id, before, ToResponse(customer));
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (IsSalesExecutive) return Forbid();
        var customer = await Db.Customers.FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer is null) return NotFound();
        var oldStatus = customer.Status;
        customer.Status = "Inactive";
        await Db.SaveChangesAsync();
        await AuditAsync("Deactivate", "Customer", id, new { status = oldStatus }, new { status = customer.Status });
        return NoContent();
    }

    private IQueryable<Customer> VisibleCustomers()
        => IsSalesExecutive ? Db.Customers.Where(c => c.AssignedTo == CurrentUserId) : Db.Customers;

    private static CustomerResponse ToResponse(Customer c)
        => new(c.CustomerId, c.CustomerCode, c.CustomerName, c.Email, c.Phone, c.CompanyName, c.Address,
            c.City, c.State, c.Status, c.CreatedDate, c.AssignedTo);
}
