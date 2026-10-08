using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Api.Models;

public sealed class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";
}
public sealed class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}
public sealed record UserResponse(string Id, string FullName, string Email, string[] Roles);

public sealed class CustomerRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string CustomerName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, RegularExpression("^[6-9][0-9]{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number.")] public string Phone { get; set; } = "";
    [StringLength(120)] public string? CompanyName { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(80)] public string? City { get; set; }
    [StringLength(80)] public string? State { get; set; }
    [RegularExpression("^(Active|Inactive)$")] public string Status { get; set; } = "Active";
    public string? AssignedTo { get; set; }
}
public sealed record CustomerResponse(int CustomerId, string CustomerCode, string CustomerName, string Email,
    string Phone, string? CompanyName, string? Address, string? City, string? State, string Status,
    DateTime CreatedDate, string AssignedTo);

public sealed class LeadRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string LeadName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, RegularExpression("^[6-9][0-9]{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number.")] public string Phone { get; set; } = "";
    [StringLength(120)] public string? CompanyName { get; set; }
    [Required, RegularExpression("^(Website|Referral|Cold Call|Event|Social)$")] public string Source { get; set; } = "Website";
    [Required, RegularExpression("^(New|Contacted|Qualified|Unqualified|Converted|Lost)$")] public string Status { get; set; } = "New";
    [Range(typeof(decimal), "0", "100000000")] public decimal ExpectedValue { get; set; }
    public string? AssignedTo { get; set; }
}
public sealed record LeadResponse(int LeadId, string LeadCode, string LeadName, string Email, string Phone,
    string? CompanyName, string Source, string Status, decimal ExpectedValue, DateTime CreatedDate, string AssignedTo);
public sealed record ConvertLeadRequest(decimal? Amount = null);

public sealed class OpportunityRequest
{
    [Required, StringLength(120)] public string OpportunityName { get; set; } = "";
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "Opportunity Amount must be greater than 0.")] public decimal Amount { get; set; }
    [Required, RegularExpression("^(Qualification|Proposal|Negotiation|Won|Lost)$")] public string Stage { get; set; } = "Qualification";
    [Range(0, 100)] public int Probability { get; set; } = 20;
    public DateTime ExpectedCloseDate { get; set; }
    public string? AssignedTo { get; set; }
}
public sealed record OpportunityResponse(int OpportunityId, string OpportunityName, int? CustomerId, int? LeadId,
    decimal Amount, string Stage, int Probability, DateTime ExpectedCloseDate, string Status,
    DateTime CreatedDate, string AssignedTo, decimal WeightedAmount);

public sealed class FollowUpRequest
{
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public DateTime FollowUpDate { get; set; }
    [Required, RegularExpression("^(Call|Meeting|Email|Task)$")] public string FollowUpType { get; set; } = "Call";
    [StringLength(500)] public string? Remarks { get; set; }
    [Required, RegularExpression("^(Planned|Completed|Missed|Cancelled)$")] public string Status { get; set; } = "Planned";
    public string? AssignedTo { get; set; }
}
public sealed record FollowUpResponse(int FollowUpId, int? CustomerId, int? LeadId, DateTime FollowUpDate,
    string FollowUpType, string? Remarks, string Status, string AssignedTo);

public sealed class ActivityRequest
{
    [Required, RegularExpression("^(Call|Meeting|Email|Task)$")] public string ActivityType { get; set; } = "Call";
    [Required, StringLength(120)] public string Subject { get; set; } = "";
    [StringLength(1000)] public string? Description { get; set; }
    public DateTime ActivityDate { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Required, RegularExpression("^(Open|Done)$")] public string Status { get; set; } = "Open";
    public string? AssignedTo { get; set; }
}
public sealed record ActivityResponse(int ActivityId, string ActivityType, string Subject, string? Description,
    DateTime ActivityDate, int? CustomerId, int? LeadId, string AssignedTo, string Status);

public sealed record DashboardResponse(int TotalCustomers, int TotalLeads, int OpenLeads,
    int TotalOpportunities, int OpenOpportunities, int WonOpportunities, int LostOpportunities,
    decimal TotalPipelineValue, decimal WeightedPipelineValue, int PendingFollowUps);
