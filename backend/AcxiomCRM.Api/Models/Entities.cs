using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Api.Models;

public sealed class ApplicationUser : IdentityUser
{
    [Required, MaxLength(100)] public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Customer
{
    public int CustomerId { get; set; }
    [Required, MaxLength(30)] public string CustomerCode { get; set; } = "";
    [Required, MaxLength(100)] public string CustomerName { get; set; } = "";
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
    [Required, MaxLength(20)] public string Phone { get; set; } = "";
    [MaxLength(120)] public string? CompanyName { get; set; }
    [MaxLength(250)] public string? Address { get; set; }
    [MaxLength(80)] public string? City { get; set; }
    [MaxLength(80)] public string? State { get; set; }
    [Required, MaxLength(20)] public string Status { get; set; } = "Active";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Required] public string CreatedBy { get; set; } = "";
    [Required] public string AssignedTo { get; set; } = "";
}

public sealed class Lead
{
    public int LeadId { get; set; }
    [Required, MaxLength(30)] public string LeadCode { get; set; } = "";
    [Required, MaxLength(100)] public string LeadName { get; set; } = "";
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
    [Required, MaxLength(20)] public string Phone { get; set; } = "";
    [MaxLength(120)] public string? CompanyName { get; set; }
    [Required, MaxLength(40)] public string Source { get; set; } = "Website";
    [Required, MaxLength(20)] public string Status { get; set; } = "New";
    [Range(0, 100000000)] public decimal ExpectedValue { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Required] public string AssignedTo { get; set; } = "";
}

public sealed class Opportunity
{
    public int OpportunityId { get; set; }
    [Required, MaxLength(120)] public string OpportunityName { get; set; } = "";
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Range(typeof(decimal), "0", "999999999999")] public decimal Amount { get; set; }
    [Required, MaxLength(30)] public string Stage { get; set; } = "Qualification";
    [Range(0, 100)] public int Probability { get; set; } = 20;
    public DateTime ExpectedCloseDate { get; set; }
    [Required, MaxLength(20)] public string Status { get; set; } = "Open";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Required] public string AssignedTo { get; set; } = "";
    public decimal WeightedAmount => Amount * Probability / 100m;
}

public sealed class FollowUp
{
    public int FollowUpId { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public DateTime FollowUpDate { get; set; }
    [Required, MaxLength(20)] public string FollowUpType { get; set; } = "Call";
    [MaxLength(500)] public string? Remarks { get; set; }
    [Required, MaxLength(20)] public string Status { get; set; } = "Planned";
    [Required] public string AssignedTo { get; set; } = "";
}

public sealed class Activity
{
    public int ActivityId { get; set; }
    [Required, MaxLength(20)] public string ActivityType { get; set; } = "Call";
    [Required, MaxLength(120)] public string Subject { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Required] public string AssignedTo { get; set; } = "";
    [Required, MaxLength(20)] public string Status { get; set; } = "Open";
}

public sealed class AuditLog
{
    public long AuditLogId { get; set; }
    public string? UserId { get; set; }
    [Required, MaxLength(40)] public string Action { get; set; } = "";
    [Required, MaxLength(50)] public string EntityName { get; set; } = "";
    [MaxLength(80)] public string? RecordId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [MaxLength(64)] public string? IpAddress { get; set; }
}
