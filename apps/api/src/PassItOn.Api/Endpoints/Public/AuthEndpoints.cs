using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassItOn.Api.Auth;
using PassItOn.Api.Contracts.Auth;
using PassItOn.Api.Domain.Entities;
using PassItOn.Api.Domain.Enums;
using PassItOn.Api.Persistence;

namespace PassItOn.Api.Endpoints.Public;

public static class AuthEndpoints
{
    private const string GoogleScheme = "Google";
    private const string FacebookScheme = "Facebook";

    public static RouteGroupBuilder MapPublicAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/register", RegisterAsync);
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/refresh", RefreshAsync);
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();

        auth.MapGet("/oauth/google/start", (HttpContext httpContext, [FromQuery] string? returnUrl) =>
            OAuthStartAsync(httpContext, providerKey: "google", scheme: GoogleScheme, returnUrl));
        auth.MapGet("/oauth/facebook/start", (HttpContext httpContext, [FromQuery] string? returnUrl) =>
            OAuthStartAsync(httpContext, providerKey: "facebook", scheme: FacebookScheme, returnUrl));
        auth.MapGet("/oauth/callback", OAuthCallbackAsync);

        return group;
    }

    // ── Register ──────────────────────────────────────────────────────────────

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest req,
        AppDbContext db,
        TokenService tokens,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Password) ||
            string.IsNullOrWhiteSpace(req.DisplayName))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["general"] = ["Email, password, and display name are required."]
            });

        var email = req.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Results.Conflict(new { error = "An account with that email already exists." });

        var user = new User
        {
            Email = email,
            DisplayName = req.DisplayName.Trim(),
        };

        var identity = new AuthIdentity
        {
            UserId = user.Id,
            Provider = AuthProvider.Local,
            ProviderSubject = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        };

        db.Users.Add(user);
        db.AuthIdentities.Add(identity);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/profile/{user.Id}", BuildAuthResponse(user, tokens, db, ct).Result);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest req,
        AppDbContext db,
        TokenService tokens,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Results.Unauthorized();

        var email = req.Email.Trim().ToLowerInvariant();

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.Status == UserStatus.Active, ct);

        if (user is null) return Results.Unauthorized();

        var identity = await db.AuthIdentities
            .FirstOrDefaultAsync(ai => ai.UserId == user.Id && ai.Provider == AuthProvider.Local, ct);

        if (identity?.PasswordHash is null || !BCrypt.Net.BCrypt.Verify(req.Password, identity.PasswordHash))
            return Results.Unauthorized();

        identity.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(await BuildAuthResponse(user, tokens, db, ct));
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshRequest req,
        AppDbContext db,
        TokenService tokens,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken)) return Results.Unauthorized();

        var tokenHash = TokenService.HashRefreshToken(req.RefreshToken);
        var session = await db.RefreshSessions
            .FirstOrDefaultAsync(rs =>
                rs.TokenHash == tokenHash &&
                rs.RevokedAt == null &&
                rs.ExpiresAt > DateTimeOffset.UtcNow, ct);

        if (session is null) return Results.Unauthorized();

        var user = await db.Users.FindAsync([session.UserId], ct);
        if (user is null || user.Status != UserStatus.Active) return Results.Unauthorized();

        // Rotate: revoke old session, issue new one
        session.RevokedAt = DateTimeOffset.UtcNow;

        var (rawNew, hashNew) = tokens.CreateRefreshToken();
        db.RefreshSessions.Add(new RefreshSession
        {
            UserId = user.Id,
            TokenHash = hashNew,
            ExpiresAt = DateTimeOffset.UtcNow.Add(tokens.RefreshTokenLifetime),
        });

        await db.SaveChangesAsync(ct);

        return Results.Ok(new AuthResponse(
            tokens.CreateAccessToken(user),
            rawNew,
            tokens.AccessTokenSeconds,
            ToUserInfo(user)));
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    private static async Task<IResult> LogoutAsync(
        [FromBody] LogoutRequest req,
        AppDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken)) return Results.NoContent();

        var tokenHash = TokenService.HashRefreshToken(req.RefreshToken);
        var session = await db.RefreshSessions
            .FirstOrDefaultAsync(rs => rs.TokenHash == tokenHash && rs.RevokedAt == null, ct);

        if (session is not null)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    // ── OAuth ─────────────────────────────────────────────────────────────────

    private static async Task<IResult> OAuthStartAsync(
        HttpContext httpContext,
        string providerKey,
        string scheme,
        string? returnUrl)
    {
        var resolvedReturnUrl = ResolveReturnUrl(httpContext, returnUrl);
        var callbackBase = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/api/auth/oauth/callback";
        var redirectUri = $"{callbackBase}?provider={providerKey}&returnUrl={Uri.EscapeDataString(resolvedReturnUrl)}";

        var props = new AuthenticationProperties { RedirectUri = redirectUri };
        await httpContext.ChallengeAsync(scheme, props);
        return Results.Empty;
    }

    private static async Task<IResult> OAuthCallbackAsync(
        HttpContext httpContext,
        AppDbContext db,
        TokenService tokens,
        [FromQuery] string provider,
        [FromQuery] string? returnUrl,
        CancellationToken ct)
    {
        var (scheme, authProvider) = provider.ToLowerInvariant() switch
        {
            "google" => (GoogleScheme, AuthProvider.Google),
            "facebook" => (FacebookScheme, AuthProvider.Facebook),
            _ => (string.Empty, (AuthProvider?)null),
        };

        if (authProvider is null)
            return Results.BadRequest(new { error = "Unsupported provider." });

        var result = await httpContext.AuthenticateAsync(scheme);
        if (!result.Succeeded)
            return TryRedirectError(returnUrl, "oauth_authentication_failed");

        var principal = result.Principal;
        if (principal is null)
            return TryRedirectError(returnUrl, "oauth_principal_missing");

        var providerSubject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var displayName = principal.FindFirstValue(ClaimTypes.Name) ?? email ?? "User";

        if (string.IsNullOrWhiteSpace(providerSubject) || string.IsNullOrWhiteSpace(email))
            return TryRedirectError(returnUrl, "oauth_required_claims_missing");

        email = email.Trim().ToLowerInvariant();

        // Find existing identity for this provider+subject
        var identity = await db.AuthIdentities
            .FirstOrDefaultAsync(ai => ai.Provider == authProvider.Value && ai.ProviderSubject == providerSubject, ct);

        User? user;
        if (identity is not null)
        {
            user = await db.Users.FindAsync([identity.UserId], ct);
            if (user is null) return TryRedirectError(returnUrl, "oauth_user_not_found");
            if (user.Status != UserStatus.Active) return TryRedirectError(returnUrl, "oauth_user_inactive");
        }
        else
        {
            // New OAuth user — create account
            user = new User { Email = email, DisplayName = displayName };
            identity = new AuthIdentity
            {
                UserId = user.Id,
                Provider = authProvider.Value,
                ProviderSubject = providerSubject,
                EmailAtProvider = email,
            };
            db.Users.Add(user);
            db.AuthIdentities.Add(identity);
        }

        identity.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var authResponse = await BuildAuthResponse(user, tokens, db, ct);

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            var redirectUrl = BuildAuthRedirectUrl(returnUrl, authResponse);
            return Results.Redirect(redirectUrl);
        }

        return Results.Ok(authResponse);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<AuthResponse> BuildAuthResponse(
        User user, TokenService tokens, AppDbContext db, CancellationToken ct)
    {
        var (raw, hash) = tokens.CreateRefreshToken();

        db.RefreshSessions.Add(new RefreshSession
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.Add(tokens.RefreshTokenLifetime),
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            tokens.CreateAccessToken(user),
            raw,
            tokens.AccessTokenSeconds,
            ToUserInfo(user));
    }

    private static UserTokenInfo ToUserInfo(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role.ToString());

    private static IResult TryRedirectError(string? returnUrl, string error)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return Results.Unauthorized();
        }

        return Results.Redirect(BuildErrorRedirectUrl(returnUrl, error));
    }

    private static string ResolveReturnUrl(HttpContext httpContext, string? requestedReturnUrl)
    {
        if (string.IsNullOrWhiteSpace(requestedReturnUrl))
        {
            return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/profile";
        }

        if (Uri.TryCreate(requestedReturnUrl, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        if (requestedReturnUrl.StartsWith('/'))
        {
            return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{requestedReturnUrl}";
        }

        return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/profile";
    }

    private static string BuildAuthRedirectUrl(string returnUrl, AuthResponse auth)
    {
        var builder = new UriBuilder(returnUrl)
        {
            Fragment =
                $"accessToken={Uri.EscapeDataString(auth.AccessToken)}" +
                $"&refreshToken={Uri.EscapeDataString(auth.RefreshToken)}" +
                $"&expiresInSeconds={auth.ExpiresInSeconds}" +
                $"&userId={Uri.EscapeDataString(auth.User.Id.ToString())}" +
                $"&userEmail={Uri.EscapeDataString(auth.User.Email)}" +
                $"&userDisplayName={Uri.EscapeDataString(auth.User.DisplayName)}" +
                $"&userRole={Uri.EscapeDataString(auth.User.Role)}"
        };

        return builder.ToString();
    }

    private static string BuildErrorRedirectUrl(string returnUrl, string error)
    {
        var builder = new UriBuilder(returnUrl)
        {
            Fragment = $"oauthError={Uri.EscapeDataString(error)}"
        };

        return builder.ToString();
    }
}
