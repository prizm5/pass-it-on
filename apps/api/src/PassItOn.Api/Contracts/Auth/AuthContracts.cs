namespace PassItOn.Api.Contracts.Auth;

// ---------- Requests ----------

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RefreshRequest(
    string RefreshToken);

public sealed record LogoutRequest(
    string RefreshToken);

// ---------- Responses ----------

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    UserTokenInfo User);

public sealed record UserTokenInfo(
    Guid Id,
    string Email,
    string DisplayName,
    string Role);

public sealed record OAuthStartResponse(
    string RedirectUrl);
