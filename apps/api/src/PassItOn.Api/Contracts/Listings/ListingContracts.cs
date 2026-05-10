using PassItOn.Api.Domain.Enums;

namespace PassItOn.Api.Contracts.Listings;

// ---------- Requests ----------

public sealed record CreateListingRequest(
    string Title,
    string Description,
    string Category,
    string? Size,
    string? AgeRange,
    ListingCondition Condition,
    ContactPreference ContactPreference);

public sealed record UpdateListingRequest(
    string? Title,
    string? Description,
    string? Category,
    string? Size,
    string? AgeRange,
    ListingCondition? Condition,
    ContactPreference? ContactPreference);

public sealed record CreateListingImageUploadRequest(
    string FileName,
    string ContentType,
    long FileSizeBytes);

public sealed record AttachListingImageRequest(
    string StorageKey,
    int? Width,
    int? Height,
    int? SortOrder);

public sealed record ReorderListingImagesRequest(
    IReadOnlyList<Guid> ImageIds);

// ---------- Query parameters ----------

public sealed record ListingsQuery(
    string? Category,
    string? Size,
    string? AgeRange,
    ListingCondition? Condition,
    string? Q,       // free-text search on title/description
    int Page = 1,
    int PageSize = 20);

// ---------- Responses ----------

public sealed record ListingResponse(
    Guid Id,
    Guid OwnerUserId,
    string OwnerDisplayName,
    string Title,
    string Description,
    string Category,
    string? Size,
    string? AgeRange,
    string Condition,
    string ContactPreference,
    string Status,
    IReadOnlyList<ListingImageResponse> Images,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ListingImageResponse(
    Guid Id,
    string PublicUrl,
    int SortOrder,
    int Width,
    int Height);

public sealed record ListingSummaryResponse(
    Guid Id,
    string Title,
    string Category,
    string? Size,
    string? AgeRange,
    string Condition,
    string Status,
    string? ThumbnailUrl,
    DateTimeOffset CreatedAt);

public sealed record ListingImageUploadUrlResponse(
    string UploadUrl,
    string StorageKey,
    string PublicUrl,
    DateTimeOffset ExpiresAt,
    long MaxFileSizeBytes,
    IReadOnlyList<string> AllowedContentTypes);
