using HomeStock.Application.Abstractions;
using HomeStock.Domain.Common;
using HomeStock.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HomeStock.Infrastructure.Data;

/// <summary>
/// EF Core context backing both ASP.NET Core Identity and the inventory domain. Implements
/// <see cref="IApplicationDbContext"/> so the application layer depends only on the abstraction.
/// Audit timestamps are maintained centrally in <see cref="SaveChangesAsync"/>.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public DbSet<InventoryItem> Items => Set<InventoryItem>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ItemTag> ItemTags => Set<ItemTag>();
    public DbSet<ItemAttachment> Attachments => Set<ItemAttachment>();
    public DbSet<ItemLoan> Loans => Set<ItemLoan>();
    public DbSet<InventoryAudit> Audits => Set<InventoryAudit>();
    public DbSet<InventoryAuditItem> AuditItems => Set<InventoryAuditItem>();
    public DbSet<ItemHistory> ItemHistory => Set<ItemHistory>();
    public DbSet<InventoryTransaction> Transactions => Set<InventoryTransaction>();
    public DbSet<ApplicationSetting> Settings => Set<ApplicationSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Property(e => e.CreatedAt).IsModified = false;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
