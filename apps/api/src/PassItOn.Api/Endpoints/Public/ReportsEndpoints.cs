using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PassItOn.Api.Auth;
using PassItOn.Api.Contracts.Reports;
using PassItOn.Api.Domain.Entities;
using PassItOn.Api.Domain.Enums;
using PassItOn.Api.Persistence;

namespace PassItOn.Api.Endpoints.Public;

public static class ReportsEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder group)
    {
        var reports = group.MapGroup("/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        reports.MapPost(string.Empty, SubmitAsync);

        return group;
    }

    private static async Task<IResult> SubmitAsync(
        ClaimsPrincipal principal,
        [FromBody] SubmitReportRequest req,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        // Validate the listing exists and is active
        var listing = await db.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == req.ListingId, ct);

        if (listing is null || listing.Status != ListingStatus.Active)
            return Results.NotFound(new { error = "Listing not found." });

        // Users cannot report their own listings
        if (listing.OwnerUserId == userId.Value)
            return Results.Conflict(new { error = "You cannot report your own listing." });

        // Prevent duplicate open reports from the same user on the same listing
        var alreadyReported = await db.ListingReports.AnyAsync(
            r => r.ListingId == req.ListingId &&
                 r.ReporterUserId == userId.Value &&
                 r.Status == ReportStatus.Open, ct);

        if (alreadyReported)
            return Results.Conflict(new { error = "You already have an open report for this listing." });

        if (req.Description is { Length: > 1000 })
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["description"] = ["Description must be 1000 characters or fewer."] });

        var report = new ListingReport
        {
            ListingId = req.ListingId,
            ReporterUserId = userId.Value,
            ReasonCode = req.ReasonCode,
            Description = req.Description?.Trim(),
            Status = ReportStatus.Open,
        };

        db.ListingReports.Add(report);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/reports/{report.Id}",
            new ReportSubmittedResponse(report.Id, "Your report has been submitted and will be reviewed by our team."));
    }
}
