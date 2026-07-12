using HomeStock.Domain.Common;
using HomeStock.Domain.Enums;

namespace HomeStock.Domain.Entities;

/// <summary>
/// A piece of work (e.g. "kitchen circuit") that draws items from inventory. While a project is
/// open its allocations reserve stock (checked out via the ledger); on completion each allocation
/// is consumed or returned.
/// </summary>
public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Open;

    public DateTime? CompletedAt { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }

    public ICollection<ProjectAllocation> Allocations { get; set; } = new List<ProjectAllocation>();
}

/// <summary>One item reserved for a project, plus how much was ultimately consumed.</summary>
public class ProjectAllocation : BaseEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;

    /// <summary>Amount currently reserved (checked out) for this project.</summary>
    public decimal QuantityAllocated { get; set; }

    /// <summary>Amount consumed at close-out (the rest is returned). Set when completing.</summary>
    public decimal QuantityConsumed { get; set; }

    /// <summary>Hint for close-out: materials default to fully consumed, tools to fully returned.</summary>
    public bool Consumable { get; set; }

    public string? Note { get; set; }
}
