namespace HomeStock.Domain.Enums;

/// <summary>Physical condition of an inventory item.</summary>
public enum ItemCondition
{
    Unknown = 0,
    New = 1,
    LikeNew = 2,
    Good = 3,
    Fair = 4,
    Poor = 5,
    ForPartsOrRepair = 6
}

/// <summary>Lifecycle / availability status of an inventory item.</summary>
public enum ItemStatus
{
    Available = 0,
    InUse = 1,
    Loaned = 2,
    Missing = 3,
    Damaged = 4,
    Disposed = 5,
    Sold = 6,
    Donated = 7,
    Archived = 8
}

/// <summary>Kind of file attached to an item.</summary>
public enum AttachmentType
{
    Photo = 0,
    SerialNumberPhoto = 1,
    Receipt = 2,
    WarrantyDocument = 3,
    Manual = 4,
    InsuranceDocument = 5,
    Other = 6
}

/// <summary>Result recorded for an item during an inventory audit.</summary>
public enum AuditItemResult
{
    Pending = 0,
    Confirmed = 1,
    Missing = 2,
    Moved = 3,
    Damaged = 4
}

/// <summary>Status of an inventory audit run.</summary>
public enum AuditStatus
{
    InProgress = 0,
    Completed = 1,
    Cancelled = 2
}

/// <summary>Type of change recorded in the item history / audit log.</summary>
public enum HistoryAction
{
    Created = 0,
    Updated = 1,
    Archived = 2,
    Restored = 3,
    Deleted = 4,
    StatusChanged = 5,
    Loaned = 6,
    Returned = 7,
    AttachmentAdded = 8,
    AttachmentRemoved = 9,
    Moved = 10
}

/// <summary>Application role names used with ASP.NET Core Identity.</summary>
public static class Roles
{
    public const string Administrator = "Administrator";
    public const string StandardUser = "StandardUser";
    public const string ReadOnly = "ReadOnly";

    public static readonly string[] All = { Administrator, StandardUser, ReadOnly };

    /// <summary>Roles permitted to create/edit/delete data.</summary>
    public const string CanEditPolicy = "CanEdit";
    /// <summary>Administrator-only actions.</summary>
    public const string AdminPolicy = "AdminOnly";
}
