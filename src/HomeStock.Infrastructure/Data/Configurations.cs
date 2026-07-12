using HomeStock.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeStock.Infrastructure.Data;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(c => c.Name).HasMaxLength(80).IsRequired();
        b.Property(c => c.Description).HasMaxLength(500);
        b.Property(c => c.Color).HasMaxLength(9);
        b.Property(c => c.Icon).HasMaxLength(50);
        b.HasIndex(c => c.Name).IsUnique();
        b.HasIndex(c => c.IsArchived);
    }
}

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> b)
    {
        b.Property(l => l.Name).HasMaxLength(120).IsRequired();
        b.Property(l => l.Description).HasMaxLength(500);
        b.Property(l => l.Code).HasMaxLength(32).IsRequired();
        b.Property(l => l.PhotoPath).HasMaxLength(400);
        b.HasIndex(l => l.Code).IsUnique();
        b.HasIndex(l => l.ParentId);

        // Restrict deletion so a parent with children cannot be removed accidentally.
        b.HasOne(l => l.Parent)
            .WithMany(l => l.Children)
            .HasForeignKey(l => l.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> b)
    {
        b.Property(i => i.Name).HasMaxLength(200).IsRequired();
        b.Property(i => i.Description).HasMaxLength(2000);
        b.Property(i => i.Notes).HasMaxLength(4000);
        b.Property(i => i.Subcategory).HasMaxLength(80);
        b.Property(i => i.Manufacturer).HasMaxLength(120);
        b.Property(i => i.Brand).HasMaxLength(120);
        b.Property(i => i.ModelNumber).HasMaxLength(120);
        b.Property(i => i.SerialNumber).HasMaxLength(120);
        b.Property(i => i.Barcode).HasMaxLength(120);
        b.Property(i => i.PurchaseLocation).HasMaxLength(200);
        b.Property(i => i.Container).HasMaxLength(120);
        b.Property(i => i.PurchasePrice).HasPrecision(18, 2);
        b.Property(i => i.EstimatedValue).HasPrecision(18, 2);

        b.HasIndex(i => i.Name);
        b.HasIndex(i => i.Barcode);
        b.HasIndex(i => i.SerialNumber);
        b.HasIndex(i => i.Status);
        b.HasIndex(i => i.IsArchived);

        // Setting a category/location to null on delete keeps the item but detaches the reference.
        b.HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(i => i.Location)
            .WithMany(l => l.Items)
            .HasForeignKey(i => i.LocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> b)
    {
        b.Property(t => t.Name).HasMaxLength(60).IsRequired();
        b.Property(t => t.NormalizedName).HasMaxLength(60).IsRequired();
        b.HasIndex(t => t.NormalizedName).IsUnique();
    }
}

public class ItemTagConfiguration : IEntityTypeConfiguration<ItemTag>
{
    public void Configure(EntityTypeBuilder<ItemTag> b)
    {
        b.HasKey(t => new { t.ItemId, t.TagId });
        b.HasOne(t => t.Item).WithMany(i => i.ItemTags).HasForeignKey(t => t.ItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(t => t.Tag).WithMany(t => t.ItemTags).HasForeignKey(t => t.TagId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ItemAttachmentConfiguration : IEntityTypeConfiguration<ItemAttachment>
{
    public void Configure(EntityTypeBuilder<ItemAttachment> b)
    {
        b.Property(a => a.OriginalFileName).HasMaxLength(260).IsRequired();
        b.Property(a => a.StoredPath).HasMaxLength(400).IsRequired();
        b.Property(a => a.ContentType).HasMaxLength(160).IsRequired();
        b.Property(a => a.Description).HasMaxLength(500);
        b.HasIndex(a => a.ItemId);
        b.HasOne(a => a.Item).WithMany(i => i.Attachments).HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ItemLoanConfiguration : IEntityTypeConfiguration<ItemLoan>
{
    public void Configure(EntityTypeBuilder<ItemLoan> b)
    {
        b.Property(l => l.BorrowerName).HasMaxLength(160).IsRequired();
        b.Property(l => l.BorrowerContact).HasMaxLength(200);
        b.Property(l => l.Notes).HasMaxLength(1000);
        b.Ignore(l => l.IsOpen);
        b.HasIndex(l => l.ItemId);
        b.HasOne(l => l.Item).WithMany(i => i.Loans).HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class InventoryAuditConfiguration : IEntityTypeConfiguration<InventoryAudit>
{
    public void Configure(EntityTypeBuilder<InventoryAudit> b)
    {
        b.Property(a => a.Notes).HasMaxLength(1000);
        b.Property(a => a.PerformedByUserId).HasMaxLength(450);
        b.HasIndex(a => a.LocationId);
        b.HasOne(a => a.Location).WithMany().HasForeignKey(a => a.LocationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class InventoryAuditItemConfiguration : IEntityTypeConfiguration<InventoryAuditItem>
{
    public void Configure(EntityTypeBuilder<InventoryAuditItem> b)
    {
        b.Property(a => a.Notes).HasMaxLength(500);
        b.HasIndex(a => a.AuditId);
        b.HasOne(a => a.Audit).WithMany(x => x.Items).HasForeignKey(a => a.AuditId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.Item).WithMany().HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.MovedToLocation).WithMany().HasForeignKey(a => a.MovedToLocationId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ItemHistoryConfiguration : IEntityTypeConfiguration<ItemHistory>
{
    public void Configure(EntityTypeBuilder<ItemHistory> b)
    {
        b.Property(h => h.UserId).HasMaxLength(450);
        b.Property(h => h.UserName).HasMaxLength(256);
        b.Property(h => h.Summary).HasMaxLength(500);
        b.HasIndex(h => h.ItemId);
        b.HasIndex(h => h.Timestamp);
        // History survives item deletion (SetNull) so the audit trail is retained.
        b.HasOne(h => h.Item).WithMany(i => i.History).HasForeignKey(h => h.ItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ApplicationSettingConfiguration : IEntityTypeConfiguration<ApplicationSetting>
{
    public void Configure(EntityTypeBuilder<ApplicationSetting> b)
    {
        b.Property(s => s.Key).HasMaxLength(100).IsRequired();
        b.Property(s => s.Value).HasMaxLength(2000);
        b.Property(s => s.Description).HasMaxLength(500);
        b.HasIndex(s => s.Key).IsUnique();
    }
}
