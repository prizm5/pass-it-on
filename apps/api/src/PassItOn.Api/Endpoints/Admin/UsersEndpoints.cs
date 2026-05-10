using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassItOn.Api.Auth;
using PassItOn.Api.Contracts.Admin;
using PassItOn.Api.Contracts.Common;
using PassItOn.Api.Domain.Entities;
using PassItOn.Api.Domain.Enums;
using PassItOn.Api.Persistence;

namespace PassItOn.Api.Endpoints.Admin;

public static class UsersEndpoints
{
    public static RouteGroupBuilder MapAdminUserEndpoints(this RouteGroupBuilder group)
    {
        var users = group.MapGroup("/users").WithTags("Admin Users");

        users.MapGet(string.Empty, ListUsersAsync);
        users.MapGet("/{userId:guid}", GetUserAsync);
        users.MapPost("/{userId:guid}/suspend", SuspendAsync);
        users.MapPost("/{userId:guid}/restore", RestoreAsync);
        users.MapPost("/{userId:guid}/delete", DeleteAsync);

        return group;
    }

    // ── GET /api/admin/users ─────────────────────────────────────────────────

    private static async Task<IResult> ListUsersAsync(
        AppDbContext db,
        CancellationToken ct,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(u => u.Email.ToLower().Contains(term) ||
                                     u.DisplayName.ToLower().Contains(term));
        }

        if (Enum.TryParse<UserStatus>(status, ignoreCase: true, out var statusFilter))
            query = query.Where(u => u.Status == statusFilter);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserSummaryResponse(
                u.Id, u.Email, u.DisplayName,
                u.Role.ToString(), u.Status.ToString(), u.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResponse<AdminUserSummaryResponse>(items, page, pageSize, total));
    }

    // ── GET /api/admin/users/{userId} ────────────────────────────────────────

    private static async Task<IResult> GetUserAsync(
        Guid userId, AppDbContext db, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Results.NotFound();

        var identities = await db.AuthIdentities
            .AsNoTracking()
            .Where(ai => ai.UserId == userId)
            .Select(ai => new AuthIdentitySummary(ai.Provider.ToString(), ai.LastLoginAt))
            .ToListAsync(ct);

        return Results.Ok(new AdminUserDetailResponse(
            user.Id, user.Email, user.DisplayName, user.PreferredName, user.Phone,
            user.Role.ToString(), user.Status.ToString(),
            user.CreatedAt, user.DeletionRequestedAt, identities));
    }

    // ── POST /api/admin/users/{userId}/suspend ───────────────────────────────

    private static async Task<IResult> SuspendAsync(
        Guid userId, ClaimsPrincipal principal,
        [FromBody] AdminActionRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var user = await db.Users.FindAsync([userId], ct);
        if (user is null || user.Status == UserStatus.Deleted) return Results.NotFound();
        if (user.Status == UserStatus.Suspended)
            return Results.Conflict(new { error = "User is already suspended." });

        user.Status = UserStatus.Suspended;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await db.RefreshSessions
            .Where(rs => rs.UserId == userId && rs.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rs => rs.RevokedAt, DateTimeOffset.UtcNow), ct);

        db.AdminAudits.Add(Audit(adminId.Value, AuditAction.UserSuspended, "User", userId.ToString(), req.Reason));
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { id = userId, status = user.Status.ToString() });
    }

    // ── POST /api/admin/users/{userId}/restore ───────────────────────────────

    private static async Task<IResult> RestoreAsync(
        Guid userId, ClaimsPrincipal principal,
        [FromBody] AdminActionRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var user = await db.Users.FindAsync([userId], ct);
        if (user is null || user.Status == UserStatus.Deleted) return Results.NotFound();
        if (user.Status != UserStatus.Suspended)
            return Results.Conflict(new { error = "User is not suspended." });

        user.Status = UserStatus.Active;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        db.AdminAudits.Add(Audit(adminId.Value, AuditAction.UserRestored, "User", userId.ToString(), req.Reason));
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { id = userId, status = user.Status.ToString() });
    }

    // ── POST /api/admin/users/{userId}/delete ────────────────────────────────

    private static async Task<IResult> DeleteAsync(
        Guid userId, ClaimsPrincipal principal,
        [FromBody] AdminActionRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var user = await db.Users.FindAsync([userId], ct);
        if (user is null || user.Status == UserStatus.Deleted) return Results.NotFound();

        // Anonymise PII before marking deleted
        user.Email = $"deleted-{userId}@deleted.invalid";
        user.DisplayName = "Deleted User";
        user.PreferredName = null;
        user.Phone = null;
        user.AvatarUrl = null;
        user.Status = UserStatus.Deleted;
        user.DeletedAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await db.RefreshSessions
            .Where(rs => rs.UserId == userId && rs.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rs => rs.RevokedAt, DateTimeOffset.UtcNow), ct);

        db.AdminAudits.Add(Audit(adminId.Value, AuditAction.UserDeleted, "User", userId.ToString(), req.Reason));
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { id = userId, status = UserStatus.Deleted.ToString() });
    }

    private static Domain.Entities.AdminAudit Audit(
        Guid adminId, AuditAction action, string targetType, string targetId, string? reason) =>
        new() { AdminUserId = adminId, Action = action, TargetType = targetType, TargetId = targetId, Reason = reason };
}
