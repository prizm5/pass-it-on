namespace PassItOn.Api.Contracts.Profile;

public sealed record ProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? PreferredName,
    string? Phone,
    string? AvatarUrl,
    string Role,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record UpdateProfileRequest(
    string? DisplayName,
    string? PreferredName,
    string? Phone);

public sealed record DeletionRequestResponse(
    string Message,
    DateTimeOffset ScheduledAt);
