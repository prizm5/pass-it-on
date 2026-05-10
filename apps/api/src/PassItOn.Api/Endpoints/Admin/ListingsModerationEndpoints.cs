using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassItOn.Api.Auth;
using PassItOn.Api.Contracts.Admin;
using PassItOn.Api.Contracts.Common;
using PassItOn.Api.Domain.Enums;
using PassItOn.Api.Persistence;

namespace PassItOn.Api.Endpoints.Admin;

public static class ListingsModerationEndpoints
{
    public static RouteGroupBuilder MapAdminListingEndpoints(this RouteGroupBuilder group)
    {
        var listings = group.MapGroup("/listings").WithTags("Admin Listings");

        listings.MapGet(string.Empty, ListListingsAsync);
        listings.MapGet("/{listingId:guid}/reports", GetListingReportsAsync);
        listings.MapPost("/{listingId:guid}/remove", RemoveAsync);
        listings.MapPost("/{listingId:guid}/restore", RestoreAsync);

        return group;
    }

    // ── GET /api/admin/listings ──────────────────────────────────────────────

    private static async Task<IResult> ListListingsAsync(
        AppDbContext db,
        CancellationToken ct,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Listings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(l => l.Title.ToLower().Contains(term));
        }

        if (Enum.TryParse<ListingStatus>(status, ignoreCase: true, out var statusFilter))
            query = query.Where(l => l.Status == statusFilter);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.OwnerUserId,
                l.Title,
                l.Category,
                l.Status,
                l.CreatedAt,
                OwnerEmail = db.Users
                    .Where(u => u.Id == l.OwnerUserId)
                    .Select(u => u.Email)
                    .FirstOrDefault() ?? string.Empty,
                ReportCount = db.ListingReports.Count(r => r.ListingId == l.Id),
            })
            .ToListAsync(ct);

        var responses = items.Select(i => new AdminListingSummaryResponse(
            i.Id, i.OwnerUserId, i.OwnerEmail, i.Title,
            i.Category, i.Status.ToString(), i.ReportCount, i.CreatedAt))
            .ToList();

        return Results.Ok(new PagedResponse<AdminListingSummaryResponse>(responses, page, pageSize, total));
    }

    // ── GET /api/admin/listings/{listingId}/reports ──────────────────────────

    private static async Task<IResult> GetListingReportsAsync(
        Guid listingId, AppDbContext db, CancellationToken ct)
    {
        var listing = await db.Listings.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);
        if (listing is null) return Results.NotFound();

        var reports = await db.ListingReports
            .AsNoTracking()
            .Where(r => r.ListingId == listingId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id, r.ReporterUserId, r.ReasonCode,
                r.Description, r.Status, r.CreatedAt,
                ReporterEmail = db.Users
                    .Where(u => u.Id == r.ReporterUserId)
                    .Select(u => u.Email)
                    .FirstOrDefault() ?? string.Empty,
            })
            .ToListAsync(ct);

        var responses = reports.Select(r => new AdminListingReportResponse(
            r.Id, r.ReporterUserId, r.ReporterEmail,
            r.ReasonCode.ToString(), r.Description, r.Status.ToString(), r.CreatedAt))
            .ToList();

        return Results.Ok(responses);
    }

    // ── POST /api/admin/listings/{listingId}/remove ──────────────────────────

    private static async Task<IResult> RemoveAsync(
        Guid listingId, ClaimsPrincipal principal,
        [FromBody] AdminActionRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var listing = await db.Listings.FindAsync([listingId], ct);
        if (listing is null) return Results.NotFound();
        if (listing.Status == ListingStatus.Removed)
            return Results.Conflict(new { error = "Listing is already removed." });

        listing.Status = ListingStatus.Removed;
        listing.RemovedAt = DateTimeOffset.UtcNow;
        listing.UpdatedAt = DateTimeOffset.UtcNow;

        db.AdminAudits.Add(new Domain.Entities.AdminAudit
        {
            AdminUserId = adminId.Value, Action = AuditAction.ListingRemoved,
            TargetType = "Listing", TargetId = listingId.ToString(), Reason = req.Reason,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { id = listingId, status = ListingStatus.Removed.ToString() });
    }

    // ── POST /api/admin/listings/{listingId}/restore ─────────────────────────

    private static async Task<IResult> RestoreAsync(
        Guid listingId, ClaimsPrincipal principal,
        [FromBody] AdminActionRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var listing = await db.Listings.FindAsync([listingId], ct);
        if (listing is null) return Results.NotFound();
        if (listing.Status != ListingStatus.Removed)
            return Results.Conflict(new { error = "Listing is not removed." });

        listing.Status = ListingStatus.Active;
        listing.RemovedAt = null;
        listing.UpdatedAt = DateTimeOffset.UtcNow;

        db.AdminAudits.Add(new Domain.Entities.AdminAudit
        {
            AdminUserId = adminId.Value, Action = AuditAction.ListingRestored,
            TargetType = "Listing", TargetId = listingId.ToString(), Reason = req.Reason,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { id = listingId, status = ListingStatus.Active.ToString() });
    }
}
