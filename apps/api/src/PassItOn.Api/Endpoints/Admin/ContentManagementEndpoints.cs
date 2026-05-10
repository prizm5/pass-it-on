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

public static class ContentManagementEndpoints
{
    public static RouteGroupBuilder MapAdminContentEndpoints(this RouteGroupBuilder group)
    {
        var content = group.MapGroup("/content").WithTags("Admin Content");

        // Bulletins
        content.MapGet("/bulletins", ListBulletinsAsync);
        content.MapPost("/bulletins", CreateBulletinAsync);
        content.MapPatch("/bulletins/{bulletinId:guid}", UpdateBulletinAsync);
        content.MapPost("/bulletins/{bulletinId:guid}/publish", PublishBulletinAsync);
        content.MapPost("/bulletins/{bulletinId:guid}/unpublish", UnpublishBulletinAsync);

        // Content pages
        content.MapGet("/pages", ListPagesAsync);
        content.MapPost("/pages", CreatePageAsync);
        content.MapPatch("/pages/{pageId:guid}", UpdatePageAsync);
        content.MapPost("/pages/{pageId:guid}/publish", PublishPageAsync);

        return group;
    }

    // ── Bulletins ────────────────────────────────────────────────────────────

    private static async Task<IResult> ListBulletinsAsync(
        AppDbContext db, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var total = await db.BulletinPosts.CountAsync(ct);
        var items = await db.BulletinPosts
            .AsNoTracking()
            .OrderByDescending(bp => bp.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(bp => new AdminBulletinResponse(
                bp.Id, bp.Title, bp.Body, bp.Status.ToString(),
                bp.PublishAt, bp.PublishedAt, bp.ExpiresAt, bp.UpdatedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResponse<AdminBulletinResponse>(items, page, pageSize, total));
    }

    private static async Task<IResult> CreateBulletinAsync(
        ClaimsPrincipal principal, [FromBody] CreateBulletinRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Body))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["general"] = ["Title and body are required."] });

        var post = new BulletinPost
        {
            Title = req.Title.Trim(),
            Body = req.Body.Trim(),
            PublishAt = req.PublishAt,
            ExpiresAt = req.ExpiresAt,
            CreatedByUserId = adminId.Value,
            UpdatedByUserId = adminId.Value,
        };
        db.BulletinPosts.Add(post);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/content/bulletins/{post.Id}",
            ToBulletinResponse(post));
    }

    private static async Task<IResult> UpdateBulletinAsync(
        Guid bulletinId, ClaimsPrincipal principal, [FromBody] UpdateBulletinRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var post = await db.BulletinPosts.FindAsync([bulletinId], ct);
        if (post is null) return Results.NotFound();

        if (req.Title is not null) post.Title = req.Title.Trim();
        if (req.Body is not null) post.Body = req.Body.Trim();
        if (req.PublishAt is not null) post.PublishAt = req.PublishAt;
        if (req.ExpiresAt is not null) post.ExpiresAt = req.ExpiresAt;
        post.UpdatedByUserId = adminId.Value;
        post.UpdatedAt = DateTimeOffset.UtcNow;

        db.AdminAudits.Add(new AdminAudit
        {
            AdminUserId = adminId.Value, Action = AuditAction.ContentUpdated,
            TargetType = "BulletinPost", TargetId = bulletinId.ToString(),
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToBulletinResponse(post));
    }

    private static async Task<IResult> PublishBulletinAsync(
        Guid bulletinId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var post = await db.BulletinPosts.FindAsync([bulletinId], ct);
        if (post is null) return Results.NotFound();
        if (post.Status == ContentStatus.Published)
            return Results.Conflict(new { error = "Bulletin is already published." });

        var now = DateTimeOffset.UtcNow;
        post.Status = ContentStatus.Published;
        post.PublishedAt = now;
        post.UpdatedByUserId = adminId.Value;
        post.UpdatedAt = now;

        db.AdminAudits.Add(new AdminAudit
        {
            AdminUserId = adminId.Value, Action = AuditAction.BulletinPublished,
            TargetType = "BulletinPost", TargetId = bulletinId.ToString(),
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToBulletinResponse(post));
    }

    private static async Task<IResult> UnpublishBulletinAsync(
        Guid bulletinId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var post = await db.BulletinPosts.FindAsync([bulletinId], ct);
        if (post is null) return Results.NotFound();
        if (post.Status != ContentStatus.Published)
            return Results.Conflict(new { error = "Bulletin is not published." });

        post.Status = ContentStatus.Draft;
        post.PublishedAt = null;
        post.UpdatedByUserId = adminId.Value;
        post.UpdatedAt = DateTimeOffset.UtcNow;

        db.AdminAudits.Add(new AdminAudit
        {
            AdminUserId = adminId.Value, Action = AuditAction.BulletinUnpublished,
            TargetType = "BulletinPost", TargetId = bulletinId.ToString(),
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToBulletinResponse(post));
    }

    // ── Content Pages ────────────────────────────────────────────────────────

    private static async Task<IResult> ListPagesAsync(
        AppDbContext db, CancellationToken ct)
    {
        var pages = await db.ContentPages
            .AsNoTracking()
            .OrderBy(cp => cp.ContentType).ThenBy(cp => cp.Slug)
            .Select(cp => new AdminContentPageResponse(
                cp.Id, cp.Slug, cp.Title, cp.Body,
                cp.ContentType.ToString(), cp.Status.ToString(),
                cp.PublishedAt, cp.UpdatedAt))
            .ToListAsync(ct);

        return Results.Ok(pages);
    }

    private static async Task<IResult> CreatePageAsync(
        ClaimsPrincipal principal, [FromBody] CreateContentPageRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Slug) || string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Body))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["general"] = ["Slug, title, and body are required."] });

        var slug = req.Slug.Trim().ToLower();
        if (await db.ContentPages.AnyAsync(cp => cp.Slug == slug, ct))
            return Results.Conflict(new { error = $"A page with slug '{slug}' already exists." });

        var page = new ContentPage
        {
            Slug = slug,
            Title = req.Title.Trim(),
            Body = req.Body.Trim(),
            ContentType = req.ContentType,
            UpdatedByUserId = adminId.Value,
        };
        db.ContentPages.Add(page);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/content/pages/{page.Id}", ToPageResponse(page));
    }

    private static async Task<IResult> UpdatePageAsync(
        Guid pageId, ClaimsPrincipal principal, [FromBody] UpdateContentPageRequest req,
        AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var page = await db.ContentPages.FindAsync([pageId], ct);
        if (page is null) return Results.NotFound();

        if (req.Title is not null) page.Title = req.Title.Trim();
        if (req.Body is not null) page.Body = req.Body.Trim();
        page.UpdatedByUserId = adminId.Value;
        page.UpdatedAt = DateTimeOffset.UtcNow;

        db.AdminAudits.Add(new AdminAudit
        {
            AdminUserId = adminId.Value, Action = AuditAction.ContentUpdated,
            TargetType = "ContentPage", TargetId = pageId.ToString(),
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToPageResponse(page));
    }

    private static async Task<IResult> PublishPageAsync(
        Guid pageId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var adminId = principal.GetUserId();
        if (adminId is null) return Results.Unauthorized();

        var page = await db.ContentPages.FindAsync([pageId], ct);
        if (page is null) return Results.NotFound();
        if (page.Status == ContentStatus.Published)
            return Results.Conflict(new { error = "Page is already published." });

        var now = DateTimeOffset.UtcNow;
        page.Status = ContentStatus.Published;
        page.PublishedAt = now;
        page.UpdatedByUserId = adminId.Value;
        page.UpdatedAt = now;

        db.AdminAudits.Add(new AdminAudit
        {
            AdminUserId = adminId.Value, Action = AuditAction.ContentUpdated,
            TargetType = "ContentPage", TargetId = pageId.ToString(),
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToPageResponse(page));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AdminBulletinResponse ToBulletinResponse(BulletinPost bp) =>
        new(bp.Id, bp.Title, bp.Body, bp.Status.ToString(),
            bp.PublishAt, bp.PublishedAt, bp.ExpiresAt, bp.UpdatedAt);

    private static AdminContentPageResponse ToPageResponse(ContentPage cp) =>
        new(cp.Id, cp.Slug, cp.Title, cp.Body,
            cp.ContentType.ToString(), cp.Status.ToString(), cp.PublishedAt, cp.UpdatedAt);
}
