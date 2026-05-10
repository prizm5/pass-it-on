using Microsoft.EntityFrameworkCore;
using PassItOn.Api.Contracts.Content;
using PassItOn.Api.Domain.Enums;
using PassItOn.Api.Persistence;

namespace PassItOn.Api.Endpoints.Public;

public static class ContentEndpoints
{
    public static RouteGroupBuilder MapContentEndpoints(this RouteGroupBuilder group)
    {
        var content = group.MapGroup("/content").WithTags("Content");

        // Returns the most recently published, non-expired bulletin post
        content.MapGet("/home-bulletin", GetActiveBulletinAsync);

        // Returns all published bulletin posts (newest first), optionally limited
        content.MapGet("/bulletins", GetBulletinsAsync);

        // Returns a single content page by slug (faq, privacy-policy, terms, etc.)
        content.MapGet("/pages/{slug}", GetPageBySlugAsync);

        return group;
    }

    // ── GET /api/content/home-bulletin ───────────────────────────────────────

    private static async Task<IResult> GetActiveBulletinAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var post = await db.BulletinPosts
            .AsNoTracking()
            .Where(bp =>
                bp.Status == ContentStatus.Published &&
                bp.PublishedAt <= now &&
                (bp.ExpiresAt == null || bp.ExpiresAt > now))
            .OrderByDescending(bp => bp.PublishedAt)
            .FirstOrDefaultAsync(ct);

        if (post is null) return Results.NoContent();

        return Results.Ok(new BulletinPostResponse(
            post.Id,
            post.Title,
            post.Body,
            post.PublishedAt,
            post.ExpiresAt));
    }

    // ── GET /api/content/bulletins ───────────────────────────────────────────

    private static async Task<IResult> GetBulletinsAsync(
        AppDbContext db,
        CancellationToken ct,
        int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);
        var now = DateTimeOffset.UtcNow;

        var posts = await db.BulletinPosts
            .AsNoTracking()
            .Where(bp =>
                bp.Status == ContentStatus.Published &&
                bp.PublishedAt <= now &&
                (bp.ExpiresAt == null || bp.ExpiresAt > now))
            .OrderByDescending(bp => bp.PublishedAt)
            .Take(limit)
            .Select(bp => new BulletinPostResponse(
                bp.Id,
                bp.Title,
                bp.Body,
                bp.PublishedAt,
                bp.ExpiresAt))
            .ToListAsync(ct);

        return Results.Ok(posts);
    }

    // ── GET /api/content/pages/{slug} ───────────────────────────────────────

    private static async Task<IResult> GetPageBySlugAsync(
        string slug,
        AppDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug)) return Results.NotFound();

        var page = await db.ContentPages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                cp => cp.Slug == slug.ToLower() && cp.Status == ContentStatus.Published,
                ct);

        if (page is null) return Results.NotFound();

        return Results.Ok(new ContentPageResponse(
            page.Id,
            page.Slug,
            page.Title,
            page.Body,
            page.ContentType.ToString(),
            page.PublishedAt));
    }
}
