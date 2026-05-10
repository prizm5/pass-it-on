using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassItOn.Api.Contracts.Admin;
using PassItOn.Api.Contracts.Common;
using PassItOn.Api.Domain.Enums;
using PassItOn.Api.Persistence;

namespace PassItOn.Api.Endpoints.Admin;

public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAdminAuditEndpoints(this RouteGroupBuilder group)
    {
        var audit = group.MapGroup("/audit").WithTags("Admin Audit");

        audit.MapGet(string.Empty, ListAuditAsync);

        return group;
    }

    // ── GET /api/admin/audit ─────────────────────────────────────────────────

    private static async Task<IResult> ListAuditAsync(
        AppDbContext db,
        CancellationToken ct,
        [FromQuery] Guid? adminUserId = null,
        [FromQuery] string? targetType = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.AdminAudits.AsNoTracking();

        if (adminUserId.HasValue)
            query = query.Where(a => a.AdminUserId == adminUserId.Value);

        if (!string.IsNullOrWhiteSpace(targetType))
            query = query.Where(a => a.TargetType == targetType);

        if (Enum.TryParse<AuditAction>(action, ignoreCase: true, out var actionFilter))
            query = query.Where(a => a.Action == actionFilter);

        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.AdminUserId, a.Action,
                a.TargetType, a.TargetId, a.Reason, a.CreatedAt,
                AdminEmail = db.Users
                    .Where(u => u.Id == a.AdminUserId)
                    .Select(u => u.Email)
                    .FirstOrDefault() ?? string.Empty,
            })
            .ToListAsync(ct);

        var responses = items.Select(a => new AdminAuditEntryResponse(
            a.Id, a.AdminUserId, a.AdminEmail,
            a.Action.ToString(), a.TargetType, a.TargetId, a.Reason, a.CreatedAt))
            .ToList();

        return Results.Ok(new PagedResponse<AdminAuditEntryResponse>(responses, page, pageSize, total));
    }
}
