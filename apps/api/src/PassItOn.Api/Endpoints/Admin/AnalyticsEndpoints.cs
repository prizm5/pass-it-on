using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassItOn.Api.Contracts.Admin;
using PassItOn.Api.Contracts.Common;
using PassItOn.Api.Domain.Enums;
using PassItOn.Api.Persistence;

namespace PassItOn.Api.Endpoints.Admin;

public static class AnalyticsEndpoints
{
    public static RouteGroupBuilder MapAdminAnalyticsEndpoints(this RouteGroupBuilder group)
    {
        var analytics = group.MapGroup("/analytics").WithTags("Admin Analytics");

        analytics.MapGet("/summary", GetSummaryAsync);
        analytics.MapGet("/events", ListEventsAsync);

        return group;
    }

    // ── GET /api/admin/analytics/summary ─────────────────────────────────────

    private static async Task<IResult> GetSummaryAsync(
        AppDbContext db, CancellationToken ct)
    {
        var totalActiveUsers = await db.Users.CountAsync(
            u => u.Status == UserStatus.Active || u.Status == UserStatus.DeletionRequested, ct);

        var totalListings = await db.Listings.CountAsync(ct);

        var activeListings = await db.Listings.CountAsync(
            l => l.Status == ListingStatus.Active, ct);

        var openReports = await db.ListingReports.CountAsync(
            r => r.Status == ReportStatus.Open || r.Status == ReportStatus.UnderReview, ct);

        var publishedBulletins = await db.BulletinPosts.CountAsync(
            bp => bp.Status == ContentStatus.Published, ct);

        return Results.Ok(new AnalyticsSummaryResponse(
            totalActiveUsers,
            totalListings,
            activeListings,
            openReports,
            publishedBulletins,
            DateTimeOffset.UtcNow));
    }

    // ── GET /api/admin/analytics/events ──────────────────────────────────────

    private static async Task<IResult> ListEventsAsync(
        AppDbContext db,
        CancellationToken ct,
        [FromQuery] string? eventName = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.AnalyticsEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(eventName))
            query = query.Where(e => e.EventName == eventName);

        if (from.HasValue)
            query = query.Where(e => e.OccurredAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.OccurredAt <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AnalyticsEventResponse(
                e.Id, e.EventName, e.ActorUserId,
                e.SubjectType, e.SubjectId, e.OccurredAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResponse<AnalyticsEventResponse>(items, page, pageSize, total));
    }
}
