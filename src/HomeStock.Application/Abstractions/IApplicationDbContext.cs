using HomeStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeStock.Application.Abstractions;

/// <summary>
/// Persistence abstraction exposed to the application layer. Implemented by the
/// Infrastructure DbContext. Keeping this interface in the application layer lets services
/// depend on an abstraction (testable, provider-agnostic) rather than the concrete context.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<InventoryItem> Items { get; }
    DbSet<Category> Categories { get; }
    DbSet<Location> Locations { get; }
    DbSet<Tag> Tags { get; }
    DbSet<ItemTag> ItemTags { get; }
    DbSet<ItemAttachment> Attachments { get; }
    DbSet<ItemLoan> Loans { get; }
    DbSet<InventoryAudit> Audits { get; }
    DbSet<InventoryAuditItem> AuditItems { get; }
    DbSet<ItemHistory> ItemHistory { get; }
    DbSet<InventoryTransaction> Transactions { get; }
    DbSet<ApplicationSetting> Settings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
