using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassItOn.Api.Auth;
using PassItOn.Api.Contracts.Profile;
using PassItOn.Api.Domain.Enums;
using PassItOn.Api.Persistence;

namespace PassItOn.Api.Endpoints.Public;

public static class ProfileEndpoints
{
    // Grace period before the background deletion job permanently removes the account.
    private static readonly TimeSpan DeletionGracePeriod = TimeSpan.FromDays(30);

    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder group)
    {
        var profile = group.MapGroup("/profile")
            .WithTags("Profile")
            .RequireAuthorization();

        profile.MapGet("/me", GetMeAsync);
        profile.MapPatch("/me", PatchMeAsync);
        profile.MapPost("/me/deletion-request", RequestDeletionAsync);
        profile.MapDelete("/me/deletion-request", CancelDeletionAsync);

        return group;
    }

    // ── GET /api/profile/me ──────────────────────────────────────────────────

    private static async Task<IResult> GetMeAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var user = await db.Users.FindAsync([userId], ct);
        if (user is null || user.Status == UserStatus.Deleted) return Results.NotFound();

        return Results.Ok(ToResponse(user));
    }

    // ── PATCH /api/profile/me ────────────────────────────────────────────────

    private static async Task<IResult> PatchMeAsync(
        ClaimsPrincipal principal,
        [FromBody] UpdateProfileRequest req,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var user = await db.Users.FindAsync([userId], ct);
        if (user is null || user.Status is UserStatus.Deleted or UserStatus.Suspended)
            return Results.NotFound();

        // Validate non-empty strings when provided
        if (req.DisplayName is not null)
        {
            var name = req.DisplayName.Trim();
            if (name.Length == 0 || name.Length > 100)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["displayName"] = ["Display name must be between 1 and 100 characters."]
                });
            user.DisplayName = name;
        }

        if (req.PreferredName is not null)
            user.PreferredName = req.PreferredName.Trim() is { Length: > 0 } v ? v : null;

        if (req.Phone is not null)
            user.Phone = req.Phone.Trim() is { Length: > 0 } v ? v : null;

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToResponse(user));
    }

    // ── POST /api/profile/me/deletion-request ───────────────────────────────

    private static async Task<IResult> RequestDeletionAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var user = await db.Users.FindAsync([userId], ct);
        if (user is null || user.Status == UserStatus.Deleted) return Results.NotFound();

        if (user.Status == UserStatus.DeletionRequested)
            return Results.Conflict(new { error = "A deletion request is already pending." });

        var scheduledAt = DateTimeOffset.UtcNow.Add(DeletionGracePeriod);
        user.Status = UserStatus.DeletionRequested;
        user.DeletionRequestedAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Revoke all active refresh sessions immediately
        await db.RefreshSessions
            .Where(rs => rs.UserId == user.Id && rs.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rs => rs.RevokedAt, DateTimeOffset.UtcNow), ct);

        return Results.Accepted(value: new DeletionRequestResponse(
            $"Your account is scheduled for deletion. You have {(int)DeletionGracePeriod.TotalDays} days to cancel.",
            scheduledAt));
    }

    // ── DELETE /api/profile/me/deletion-request (cancel) ────────────────────

    private static async Task<IResult> CancelDeletionAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var user = await db.Users.FindAsync([userId], ct);
        if (user is null || user.Status == UserStatus.Deleted) return Results.NotFound();

        if (user.Status != UserStatus.DeletionRequested)
            return Results.Conflict(new { error = "No pending deletion request." });

        user.Status = UserStatus.Active;
        user.DeletionRequestedAt = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { message = "Deletion request cancelled. Your account is active again." });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ProfileResponse ToResponse(Domain.Entities.User user) =>
        new(user.Id,
            user.Email,
            user.DisplayName,
            user.PreferredName,
            user.Phone,
            user.AvatarUrl,
            user.Role.ToString(),
            user.Status.ToString(),
            user.CreatedAt);
}

