using PassItOn.Api.Domain.Enums;

namespace PassItOn.Api.Domain.Entities;

public sealed class Listing
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OwnerUserId { get; init; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? AgeRange { get; set; }
    public ListingCondition Condition { get; set; }
    public ContactPreference ContactPreference { get; set; } = ContactPreference.ProfileContact;
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}

public sealed class ListingImage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ListingId { get; init; }
    public string StorageKey { get; init; } = string.Empty;
    public string PublicUrl { get; init; } = string.Empty;
    public int SortOrder { get; set; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ListingReport
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ListingId { get; init; }
    public Guid ReporterUserId { get; init; }
    public ReportReasonCode ReasonCode { get; init; }
    public string? Description { get; init; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByAdminUserId { get; set; }
}
