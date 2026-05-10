using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassItOn.Api.Auth;
using PassItOn.Api.Contracts.Admin;
using PassItOn.Api.Contracts.Common;
using PassItOn.Api.Domain.Enums;
using PassItOn.Api.Persistence;

namespace PassItOn.Api.Endpoints.Admin;

public static class ReportsReviewEndpoints
{
    public static RouteGroupBuilder MapAdminReportEndpoints(this RouteGroupBuilder group)
    {
        var reports = group.MapGroup("/reports").WithTags("Admin Reports");

        reports.MapGet(string.Empty, ListReportsAsync);
        reports.MapGet("/{reportId:guid}", GetReportAsync);
        reports.MapPost("/{reportId:guid}/resolve", ResolveAsync);
        reports.MapPost("/{reportId:guid}/dismiss", DismissAsync);

        return group;
    }

    // ── GET /api/admin/reports ───────────────────────────────────────────────

    private static async Task<IResult> ListReportsAsync(
        AppDbContext db,
        CancellationToken ct,
        [FromQuery] string? status = "Open",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ListingReports.AsNoTracking();

        if (Enum.TryParse<ReportStatus>(status, ignoreCase: true, out var statusFilter))
            query = query.Where(r => r.Status == statusFilter);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id, r.ListingId, r.ReporterUserId,
                r.ReasonCode, r.Description, r.Status,
                r.CreatedAt, r.ReviewedAt, r.ReviewedByAdminUserId,
                ListingTitle = db.Listings
                    .Where(l => l.Id == r.ListingId)
                    .Select(l => l.Title)
                    .FirstOrDefault() ?? string.Empty,
                ReporterEmail = db.Users
                    .Where(u => u.Id == r.ReporterUserId)
                    .Select(u => u.Email)
                    .FirstOrDefault() ?? string.Empty,
            })
            .ToListAsync(ct);

        var responses = items.Select(i => new AdminReportDetailResponse(
            i.Id, i.ListingId, i.ListingTitle, i.ReporterUserId, i.ReporterEmail,
            i.ReasonCode.ToString(), i.Description, i.Status.ToString(),
            i.CreatedAt, i.ReviewedAt, i.ReviewedByAdminUserId))
            .ToList();

        return Results.Ok(new PagedResponse<AdminReportDetailResponse>(responses, page, pageSize, total));
    }

    // ── GET /api/admin/reports/{reportId} ────────────────────────────────────

    private static async Task<IResult> GetReportAsync(
        Guid reportId, AppDbContext db, CancellationToken ct)
    {
        var r = await db.ListingReports.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (r is null) return Results.NotFound();

        var listingTitle = await db.Listings.AsNoTracking()
            .Where(l => l.Id == r.ListingId).Select(l => l.Title).FirstOrDefaultAsync(ct) ?? string.Empty;
        var reporterEmail = await db.Users.AsNoTracking()
            .Where(u => u.Id == r.ReporterUserId).Select(u => u.Email).FirstOrDefaultAsync(ct) ?? string.Empty;

        return Results.Ok(new AdminReportDetailResponse(
            r.Id, r.ListingId, listingTitle, r.ReporterUserId, reporterEmail,
            r.ReasonCode.ToString(), r.Description, r.Status.ToString(),
            r.CreatedAt, r.ReviewedAt, r.ReviewedByAdminUserId));
    }

    // ── POST /api/admin/reports/{reportId}/resolve ───────────────────────────

    private static async Task<IResult> ResolveAsync(
        Guid reportId, ClaimsPrincipal principal,
        [FromBody] ResolveReportRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var report = await db.ListingReports.FindAsync([reportId], ct);
        if (report is null) return Results.NotFound();
        if (report.Status is ReportStatus.Resolved or ReportStatus.Dismissed)
            return Results.Conflict(new { error = "Report is already closed." });

        var now = DateTimeOffset.UtcNow;
        report.Status = ReportStatus.Resolved;
        report.ReviewedAt = now;
        report.ReviewedByAdminUserId = adminId.Value;

        if (req.RemoveListing)
        {
            var listing = await db.Listings.FindAsync([report.ListingId], ct);
            if (listing is not null && listing.Status != ListingStatus.Removed)
            {
                listing.Status = ListingStatus.Removed;
                listing.RemovedAt = now;
                listing.UpdatedAt = now;
                db.AdminAudits.Add(new Domain.Entities.AdminAudit
                {
                    AdminUserId = adminId.Value, Action = AuditAction.ListingRemoved,
                    TargetType = "Listing", TargetId = listing.Id.ToString(), Reason = req.Reason,
                });
            }
        }

        db.AdminAudits.Add(new Domain.Entities.AdminAudit
        {
            AdminUserId = adminId.Value, Action = AuditAction.ReportResolved,
            TargetType = "ListingReport", TargetId = reportId.ToString(), Reason = req.Reason,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { id = reportId, status = ReportStatus.Resolved.ToString() });
    }

    // ── POST /api/admin/reports/{reportId}/dismiss ───────────────────────────

    private static async Task<IResult> DismissAsync(
        Guid reportId, ClaimsPrincipal principal,
        [FromBody] DismissReportRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var report = await db.ListingReports.FindAsync([reportId], ct);
        if (report is null) return Results.NotFound();
        if (report.Status is ReportStatus.Resolved or ReportStatus.Dismissed)
            return Results.Conflict(new { error = "Report is already closed." });

        var now = DateTimeOffset.UtcNow;
        report.Status = ReportStatus.Dismissed;
        report.ReviewedAt = now;
        report.ReviewedByAdminUserId = adminId.Value;

        db.AdminAudits.Add(new Domain.Entities.AdminAudit
        {
            AdminUserId = adminId.Value, Action = AuditAction.ReportDismissed,
            TargetType = "ListingReport", TargetId = reportId.ToString(), Reason = req.Reason,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { id = reportId, status = ReportStatus.Dismissed.ToString() });
    }
}
