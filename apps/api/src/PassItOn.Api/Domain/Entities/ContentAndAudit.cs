using PassItOn.Api.Domain.Enums;

namespace PassItOn.Api.Domain.Entities;

public sealed class BulletinPost
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public DateTimeOffset? PublishAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public Guid CreatedByUserId { get; init; }
    public Guid UpdatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ContentPage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public ContentType ContentType { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AnalyticsEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string EventName { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }
    public string SubjectType { get; init; } = string.Empty;
    public string? SubjectId { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AdminAudit
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AdminUserId { get; init; }
    public AuditAction Action { get; init; }
    public string TargetType { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
