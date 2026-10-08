using AcxiomCRM.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Data;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Customer>().HasIndex(x => x.CustomerCode).IsUnique();
        builder.Entity<Customer>().HasIndex(x => x.Email).IsUnique();
        builder.Entity<Customer>().HasIndex(x => x.Phone).IsUnique();
        builder.Entity<Lead>().HasIndex(x => x.LeadCode).IsUnique();
        builder.Entity<Opportunity>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Opportunity>().Ignore(x => x.WeightedAmount);
        builder.Entity<Lead>().Property(x => x.ExpectedValue).HasPrecision(18, 2);
        builder.Entity<AuditLog>().HasIndex(x => x.CreatedDate);
    }
}
