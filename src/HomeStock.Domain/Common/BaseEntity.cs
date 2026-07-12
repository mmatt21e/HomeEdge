namespace HomeStock.Domain.Common;

/// <summary>
/// Base class for persistent entities. Provides a surrogate key and audit timestamps.
/// Timestamps are maintained centrally by the DbContext SaveChanges override.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    /// <summary>UTC creation time. Set once when the row is first inserted.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC time of the most recent update.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Marker interface for entities that support soft deletion (archival) rather than
/// physical removal, preserving history and referential integrity.
/// </summary>
public interface ISoftDeletable
{
    bool IsArchived { get; set; }
    DateTime? ArchivedAt { get; set; }
}
