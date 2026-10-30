using Microsoft.EntityFrameworkCore;

namespace IntentBlazor.StaffBusiness;

public sealed class BusinessDbContext(DbContextOptions<BusinessDbContext> options) : DbContext(options)
{
    public DbSet<BusinessJob> Jobs => Set<BusinessJob>();
    public DbSet<StaffMember> Staff => Set<StaffMember>();
    public DbSet<JobOperation> Operations => Set<JobOperation>();
    public DbSet<BusinessReceipt> Receipts => Set<BusinessReceipt>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<BusinessJob>().Property(job => job.Version).IsConcurrencyToken();
        model.Entity<BusinessJob>().HasOne<StaffMember>().WithMany().HasForeignKey(job => job.AssigneeId);
        model.Entity<BusinessReceipt>().HasKey(receipt => new { receipt.OwnerId, receipt.RequestId });
        model.Entity<JobOperation>().HasIndex(operation => new { operation.OwnerId, operation.RequestId }).IsUnique();
    }
}
public sealed class BusinessJob
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Material { get; set; } = "";
    public int WaitingDays { get; set; }
    public string Notes { get; set; } = "";
    public string? AssigneeId { get; set; }
    public long Version { get; set; } = 1;
}
public sealed class StaffMember { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
public sealed class JobOperation
{
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string JobId { get; set; } = "";
    public string Description { get; set; } = "";
    public long Version { get; set; }
    public long UtcTicks { get; set; }
}
public sealed class BusinessReceipt
{
    public string OwnerId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string ResultJson { get; set; } = "";
}
