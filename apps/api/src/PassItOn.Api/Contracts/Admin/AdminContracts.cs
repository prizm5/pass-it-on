using PassItOn.Api.Domain.Enums;

namespace PassItOn.Api.Contracts.Admin;

// ── Users ─────────────────────────────────────────────────────────────────────

public sealed record AdminUserSummaryResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record AdminUserDetailResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? PreferredName,
    string? Phone,
    string Role,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeletionRequestedAt,
    IReadOnlyList<AuthIdentitySummary> AuthIdentities);

public sealed record AuthIdentitySummary(
    string Provider,
    DateTimeOffset? LastLoginAt);

public sealed record AdminActionRequest(string Reason);

// ── Listings ──────────────────────────────────────────────────────────────────

public sealed record AdminListingSummaryResponse(
    Guid Id,
    Guid OwnerUserId,
    string OwnerEmail,
    string Title,
    string Category,
    string Status,
    int ReportCount,
    DateTimeOffset CreatedAt);

public sealed record AdminListingReportResponse(
    Guid Id,
    Guid ReporterUserId,
    string ReporterEmail,
    string ReasonCode,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt);

// ── Reports ───────────────────────────────────────────────────────────────────

public sealed record AdminReportDetailResponse(
    Guid Id,
    Guid ListingId,
    string ListingTitle,
    Guid ReporterUserId,
    string ReporterEmail,
    string ReasonCode,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    Guid? ReviewedByAdminUserId);

public sealed record ResolveReportRequest(
    string? Reason,
    bool RemoveListing = false);

public sealed record DismissReportRequest(string? Reason);

// ── Content ───────────────────────────────────────────────────────────────────

public sealed record AdminBulletinResponse(
    Guid Id,
    string Title,
    string Body,
    string Status,
    DateTimeOffset? PublishAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateBulletinRequest(
    string Title,
    string Body,
    DateTimeOffset? PublishAt,
    DateTimeOffset? ExpiresAt);

public sealed record UpdateBulletinRequest(
    string? Title,
    string? Body,
    DateTimeOffset? PublishAt,
    DateTimeOffset? ExpiresAt);

public sealed record AdminContentPageResponse(
    Guid Id,
    string Slug,
    string Title,
    string Body,
    string ContentType,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateContentPageRequest(
    string Slug,
    string Title,
    string Body,
    ContentType ContentType);

public sealed record UpdateContentPageRequest(
    string? Title,
    string? Body);

// ── Analytics ─────────────────────────────────────────────────────────────────

public sealed record AnalyticsSummaryResponse(
    int TotalActiveUsers,
    int TotalListings,
    int ActiveListings,
    int OpenReports,
    int PublishedBulletins,
    DateTimeOffset AsOf);

public sealed record AnalyticsEventResponse(
    Guid Id,
    string EventName,
    Guid? ActorUserId,
    string SubjectType,
    string? SubjectId,
    DateTimeOffset OccurredAt);

// ── Audit ─────────────────────────────────────────────────────────────────────

public sealed record AdminAuditEntryResponse(
    Guid Id,
    Guid AdminUserId,
    string AdminEmail,
    string Action,
    string TargetType,
    string TargetId,
    string? Reason,
    DateTimeOffset CreatedAt);
