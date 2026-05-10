namespace PassItOn.Api.Contracts.Content;

public sealed record BulletinPostResponse(
    Guid Id,
    string Title,
    string Body,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ExpiresAt);

public sealed record ContentPageResponse(
    Guid Id,
    string Slug,
    string Title,
    string Body,
    string ContentType,
    DateTimeOffset? PublishedAt);
