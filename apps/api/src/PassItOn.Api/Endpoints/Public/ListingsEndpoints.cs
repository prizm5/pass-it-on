using System.Security.Claims;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PassItOn.Api.Auth;
using PassItOn.Api.Contracts.Common;
using PassItOn.Api.Contracts.Listings;
using PassItOn.Api.Domain.Entities;
using PassItOn.Api.Domain.Enums;
using PassItOn.Api.Images;
using PassItOn.Api.Persistence;

namespace PassItOn.Api.Endpoints.Public;

public static class ListingsEndpoints
{
    private static readonly HashSet<string> AllowedImageContentTypes =
        ["image/jpeg", "image/png"];

    public static RouteGroupBuilder MapListingEndpoints(this RouteGroupBuilder group)
    {
        var listings = group.MapGroup("/listings").WithTags("Listings");

        // Public browse — no auth required
        listings.MapGet(string.Empty, BrowseAsync);
        listings.MapGet("/{listingId:guid}", GetByIdAsync);

        // Owner routes — auth required
        listings.MapGet("/me", GetMineAsync).RequireAuthorization();
        listings.MapPost(string.Empty, CreateAsync).RequireAuthorization();
        listings.MapPatch("/{listingId:guid}", UpdateAsync).RequireAuthorization();
        listings.MapPost("/{listingId:guid}/activate", ActivateAsync).RequireAuthorization();
        listings.MapPost("/{listingId:guid}/archive", ArchiveAsync).RequireAuthorization();
        listings.MapPost("/{listingId:guid}/mark-unavailable", MarkUnavailableAsync).RequireAuthorization();

        listings.MapPost("/{listingId:guid}/images/upload-url", CreateImageUploadUrlAsync).RequireAuthorization();
        listings.MapPost("/{listingId:guid}/images", AttachImageAsync).RequireAuthorization();
        listings.MapPost("/{listingId:guid}/images/reorder", ReorderImagesAsync).RequireAuthorization();
        listings.MapDelete("/{listingId:guid}/images/{imageId:guid}", DeleteImageAsync).RequireAuthorization();

        return group;
    }

    // ── GET /api/listings ────────────────────────────────────────────────────

    private static async Task<IResult> BrowseAsync(
        [AsParameters] ListingsQuery query,
        AppDbContext db,
        CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Listings
            .Where(l => l.Status == ListingStatus.Active)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Category))
            q = q.Where(l => l.Category == query.Category);

        if (!string.IsNullOrWhiteSpace(query.Size))
            q = q.Where(l => l.Size == query.Size);

        if (!string.IsNullOrWhiteSpace(query.AgeRange))
            q = q.Where(l => l.AgeRange == query.AgeRange);

        if (query.Condition.HasValue)
            q = q.Where(l => l.Condition == query.Condition.Value);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToLower();
            q = q.Where(l => l.Title.ToLower().Contains(term) ||
                              l.Description.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new ListingSummaryResponse(
                l.Id,
                l.Title,
                l.Category,
                l.Size,
                l.AgeRange,
                l.Condition.ToString(),
                l.Status.ToString(),
                db.ListingImages
                    .Where(i => i.ListingId == l.Id)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.PublicUrl)
                    .FirstOrDefault(),
                l.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResponse<ListingSummaryResponse>(items, page, pageSize, total));
    }

    // ── GET /api/listings/{id} ───────────────────────────────────────────────

    private static async Task<IResult> GetByIdAsync(
        Guid listingId,
        AppDbContext db,
        CancellationToken ct)
    {
        var listing = await db.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);

        if (listing is null) return Results.NotFound();

        // Only active listings are visible to the public
        if (listing.Status != ListingStatus.Active) return Results.NotFound();

        var owner = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == listing.OwnerUserId, ct);

        var images = await db.ListingImages
            .AsNoTracking()
            .Where(i => i.ListingId == listingId)
            .OrderBy(i => i.SortOrder)
            .Select(i => new ListingImageResponse(i.Id, i.PublicUrl, i.SortOrder, i.Width, i.Height))
            .ToListAsync(ct);

        return Results.Ok(ToDetailResponse(listing, owner?.DisplayName ?? "Unknown", images));
    }

    // ── GET /api/listings/me ─────────────────────────────────────────────────

    private static async Task<IResult> GetMineAsync(
        ClaimsPrincipal principal,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        page = Math.Max(1, page == 0 ? 1 : page);
        pageSize = Math.Clamp(pageSize == 0 ? 20 : pageSize, 1, 100);

        var q = db.Listings
            .AsNoTracking()
            .Where(l => l.OwnerUserId == userId.Value &&
                        l.Status != ListingStatus.Removed);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new ListingSummaryResponse(
                l.Id,
                l.Title,
                l.Category,
                l.Size,
                l.AgeRange,
                l.Condition.ToString(),
                l.Status.ToString(),
                db.ListingImages
                    .Where(i => i.ListingId == l.Id)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.PublicUrl)
                    .FirstOrDefault(),
                l.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResponse<ListingSummaryResponse>(items, page, pageSize, total));
    }

    // ── POST /api/listings ───────────────────────────────────────────────────

    private static async Task<IResult> CreateAsync(
        ClaimsPrincipal principal,
        [FromBody] CreateListingRequest req,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var errors = ValidateCreate(req);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var listing = new Listing
        {
            OwnerUserId = userId.Value,
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            Category = req.Category.Trim(),
            Size = req.Size?.Trim(),
            AgeRange = req.AgeRange?.Trim(),
            Condition = req.Condition,
            ContactPreference = req.ContactPreference,
            Status = ListingStatus.Draft,
        };

        db.Listings.Add(listing);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/listings/{listing.Id}",
            ToDetailResponse(listing, string.Empty, []));
    }

    // ── PATCH /api/listings/{id} ─────────────────────────────────────────────

    private static async Task<IResult> UpdateAsync(
        Guid listingId,
        ClaimsPrincipal principal,
        [FromBody] UpdateListingRequest req,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var listing = await db.Listings.FindAsync([listingId], ct);
        if (listing is null) return Results.NotFound();
        if (listing.OwnerUserId != userId.Value) return Results.Forbid();
        if (listing.Status is ListingStatus.Removed or ListingStatus.Archived)
            return Results.Conflict(new { error = "Cannot edit a removed or archived listing." });

        if (req.Title is not null)
        {
            var t = req.Title.Trim();
            if (t.Length is 0 or > 200)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["title"] = ["Title must be between 1 and 200 characters."] });
            listing.Title = t;
        }

        if (req.Description is not null)
        {
            var d = req.Description.Trim();
            if (d.Length is 0 or > 4000)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["description"] = ["Description must be between 1 and 4000 characters."] });
            listing.Description = d;
        }

        if (req.Category is not null) listing.Category = req.Category.Trim();
        if (req.Size is not null) listing.Size = req.Size.Trim() is { Length: > 0 } v ? v : null;
        if (req.AgeRange is not null) listing.AgeRange = req.AgeRange.Trim() is { Length: > 0 } v ? v : null;
        if (req.Condition.HasValue) listing.Condition = req.Condition.Value;
        if (req.ContactPreference.HasValue) listing.ContactPreference = req.ContactPreference.Value;

        listing.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var images = await db.ListingImages
            .AsNoTracking()
            .Where(i => i.ListingId == listing.Id)
            .OrderBy(i => i.SortOrder)
            .Select(i => new ListingImageResponse(i.Id, i.PublicUrl, i.SortOrder, i.Width, i.Height))
            .ToListAsync(ct);

        var owner = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == listing.OwnerUserId, ct);

        return Results.Ok(ToDetailResponse(listing, owner?.DisplayName ?? string.Empty, images));
    }

    // ── Status transitions ───────────────────────────────────────────────────

    private static async Task<IResult> ActivateAsync(
        Guid listingId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        await TransitionAsync(listingId, principal, db, ct,
            allowed: [ListingStatus.Draft, ListingStatus.Unavailable],
            next: ListingStatus.Active);

    private static async Task<IResult> ArchiveAsync(
        Guid listingId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        await TransitionAsync(listingId, principal, db, ct,
            allowed: [ListingStatus.Active, ListingStatus.Draft, ListingStatus.Unavailable],
            next: ListingStatus.Archived,
            stamp: (l, now) => l.ArchivedAt = now);

    private static async Task<IResult> MarkUnavailableAsync(
        Guid listingId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        await TransitionAsync(listingId, principal, db, ct,
            allowed: [ListingStatus.Active],
            next: ListingStatus.Unavailable);

    private static async Task<IResult> TransitionAsync(
        Guid listingId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken ct,
        ListingStatus[] allowed,
        ListingStatus next,
        Action<Listing, DateTimeOffset>? stamp = null)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var listing = await db.Listings.FindAsync([listingId], ct);
        if (listing is null) return Results.NotFound();
        if (listing.OwnerUserId != userId.Value) return Results.Forbid();
        if (!allowed.Contains(listing.Status))
            return Results.Conflict(new { error = $"Cannot transition from '{listing.Status}' to '{next}'." });

        var now = DateTimeOffset.UtcNow;
        listing.Status = next;
        listing.UpdatedAt = now;
        stamp?.Invoke(listing, now);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { id = listing.Id, status = listing.Status.ToString() });
    }

    // ── Images ───────────────────────────────────────────────────────────────

    private static async Task<IResult> CreateImageUploadUrlAsync(
        Guid listingId,
        ClaimsPrincipal principal,
        [FromBody] CreateListingImageUploadRequest req,
        AppDbContext db,
        IAmazonS3 s3,
        IOptions<S3ImageStorageOptions> options,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var storageOptions = options.Value;
        var disabled = ValidateStorageIsConfigured(storageOptions);
        if (disabled is not null) return disabled;

        var listing = await db.Listings.FindAsync([listingId], ct);
        if (listing is null) return Results.NotFound();
        if (listing.OwnerUserId != userId.Value) return Results.Forbid();

        if (req.FileSizeBytes <= 0 || req.FileSizeBytes > storageOptions.MaxFileSizeBytes)
        {
            return Results.BadRequest(new
            {
                error = $"Image size must be between 1 byte and {storageOptions.MaxFileSizeBytes} bytes."
            });
        }

        if (!AllowedImageContentTypes.Contains(req.ContentType.Trim().ToLowerInvariant()))
            return Results.BadRequest(new { error = "Only image/jpeg and image/png are supported." });

        var imageCount = await db.ListingImages.CountAsync(i => i.ListingId == listingId, ct);
        if (imageCount >= storageOptions.MaxImagesPerListing)
        {
            return Results.Conflict(new
            {
                error = $"A listing can have up to {storageOptions.MaxImagesPerListing} images."
            });
        }

        var extension = req.ContentType.Trim().ToLowerInvariant() switch
        {
            "image/png" => ".png",
            _ => ".jpg",
        };

        var normalizedPrefix = storageOptions.KeyPrefix.Trim('/');
        var storageKey = $"{normalizedPrefix}/{listingId}/{Guid.NewGuid():N}{extension}";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(storageOptions.PresignedUrlMinutes, 1, 60));

        var uploadUrl = s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = storageOptions.BucketName,
            Key = storageKey,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = req.ContentType.Trim().ToLowerInvariant(),
            Protocol = Protocol.HTTPS,
        });

        var publicUrl = BuildPublicUrl(storageOptions, storageKey);

        return Results.Ok(new ListingImageUploadUrlResponse(
            uploadUrl,
            storageKey,
            publicUrl,
            expiresAt,
            storageOptions.MaxFileSizeBytes,
            AllowedImageContentTypes.ToArray()));
    }

    private static async Task<IResult> AttachImageAsync(
        Guid listingId,
        ClaimsPrincipal principal,
        [FromBody] AttachListingImageRequest req,
        AppDbContext db,
        IAmazonS3 s3,
        IOptions<S3ImageStorageOptions> options,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var storageOptions = options.Value;
        var disabled = ValidateStorageIsConfigured(storageOptions);
        if (disabled is not null) return disabled;

        var listing = await db.Listings.FindAsync([listingId], ct);
        if (listing is null) return Results.NotFound();
        if (listing.OwnerUserId != userId.Value) return Results.Forbid();

        var storageKey = req.StorageKey.Trim();
        if (!IsValidStorageKey(storageOptions, listingId, storageKey))
            return Results.BadRequest(new { error = "Invalid storage key for listing image." });

        var imageCount = await db.ListingImages.CountAsync(i => i.ListingId == listingId, ct);
        if (imageCount >= storageOptions.MaxImagesPerListing)
        {
            return Results.Conflict(new
            {
                error = $"A listing can have up to {storageOptions.MaxImagesPerListing} images."
            });
        }

        GetObjectMetadataResponse metadata;
        try
        {
            metadata = await s3.GetObjectMetadataAsync(storageOptions.BucketName, storageKey, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Results.BadRequest(new { error = "Uploaded file was not found in storage." });
        }

        var metadataContentType = metadata.Headers.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedImageContentTypes.Contains(metadataContentType))
            return Results.BadRequest(new { error = "Uploaded file content type is not allowed." });

        if (metadata.Headers.ContentLength <= 0 || metadata.Headers.ContentLength > storageOptions.MaxFileSizeBytes)
            return Results.BadRequest(new { error = "Uploaded file size is out of range." });

        var nextSortOrder = req.SortOrder ?? await db.ListingImages
            .Where(i => i.ListingId == listingId)
            .Select(i => i.SortOrder)
            .DefaultIfEmpty(0)
            .MaxAsync(ct) + 1;

        var image = new ListingImage
        {
            ListingId = listingId,
            StorageKey = storageKey,
            PublicUrl = BuildPublicUrl(storageOptions, storageKey),
            SortOrder = Math.Max(0, nextSortOrder),
            Width = Math.Max(0, req.Width ?? 0),
            Height = Math.Max(0, req.Height ?? 0),
        };

        db.ListingImages.Add(image);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new ListingImageResponse(image.Id, image.PublicUrl, image.SortOrder, image.Width, image.Height));
    }

    private static async Task<IResult> DeleteImageAsync(
        Guid listingId,
        Guid imageId,
        ClaimsPrincipal principal,
        AppDbContext db,
        IAmazonS3 s3,
        IOptions<S3ImageStorageOptions> options,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var listing = await db.Listings.FindAsync([listingId], ct);
        if (listing is null) return Results.NotFound();
        if (listing.OwnerUserId != userId.Value) return Results.Forbid();

        var image = await db.ListingImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ListingId == listingId, ct);
        if (image is null) return Results.NotFound();

        var storageOptions = options.Value;
        if (storageOptions.Enabled && !string.IsNullOrWhiteSpace(storageOptions.BucketName) && !string.IsNullOrWhiteSpace(image.StorageKey))
        {
            try
            {
                await s3.DeleteObjectAsync(storageOptions.BucketName, image.StorageKey, ct);
            }
            catch (AmazonS3Exception)
            {
            }
        }

        db.ListingImages.Remove(image);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> ReorderImagesAsync(
        Guid listingId,
        ClaimsPrincipal principal,
        [FromBody] ReorderListingImagesRequest req,
        AppDbContext db,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        if (userId is null) return Results.Unauthorized();

        var listing = await db.Listings.FindAsync([listingId], ct);
        if (listing is null) return Results.NotFound();
        if (listing.OwnerUserId != userId.Value) return Results.Forbid();

        if (req.ImageIds is null || req.ImageIds.Count == 0)
            return Results.BadRequest(new { error = "ImageIds must include at least one image id." });

        var images = await db.ListingImages
            .Where(i => i.ListingId == listingId)
            .ToListAsync(ct);

        if (images.Count == 0)
            return Results.BadRequest(new { error = "Listing has no images to reorder." });

        if (req.ImageIds.Count != images.Count)
            return Results.BadRequest(new { error = "ImageIds must include all listing image ids exactly once." });

        var requested = req.ImageIds.ToHashSet();
        if (requested.Count != req.ImageIds.Count)
            return Results.BadRequest(new { error = "ImageIds cannot include duplicates." });

        var current = images.Select(i => i.Id).ToHashSet();
        if (!requested.SetEquals(current))
            return Results.BadRequest(new { error = "ImageIds do not match listing images." });

        var sortOrder = 0;
        var byId = images.ToDictionary(i => i.Id);
        foreach (var imageId in req.ImageIds)
        {
            byId[imageId].SortOrder = sortOrder;
            sortOrder += 1;
        }

        await db.SaveChangesAsync(ct);

        var response = req.ImageIds
            .Select(id => byId[id])
            .Select(i => new ListingImageResponse(i.Id, i.PublicUrl, i.SortOrder, i.Width, i.Height))
            .ToList();

        return Results.Ok(response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, string[]> ValidateCreate(CreateListingRequest req)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 200)
            errors["title"] = ["Title is required and must be 200 characters or fewer."];
        if (string.IsNullOrWhiteSpace(req.Description) || req.Description.Length > 4000)
            errors["description"] = ["Description is required and must be 4000 characters or fewer."];
        if (string.IsNullOrWhiteSpace(req.Category) || req.Category.Length > 100)
            errors["category"] = ["Category is required and must be 100 characters or fewer."];
        return errors;
    }

    private static IResult? ValidateStorageIsConfigured(S3ImageStorageOptions options)
    {
        if (!options.Enabled)
            return Results.Conflict(new { error = "Image storage is disabled." });

        if (string.IsNullOrWhiteSpace(options.BucketName) || string.IsNullOrWhiteSpace(options.Region))
            return Results.Problem(statusCode: 500, title: "Image storage misconfigured.");

        return null;
    }

    private static string BuildPublicUrl(S3ImageStorageOptions options, string storageKey)
    {
        var normalizedKey = storageKey.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(options.PublicBaseUrl))
            return $"{options.PublicBaseUrl.TrimEnd('/')}/{normalizedKey}";

        return $"https://{options.BucketName}.s3.{options.Region}.amazonaws.com/{normalizedKey}";
    }

    private static bool IsValidStorageKey(S3ImageStorageOptions options, Guid listingId, string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return false;

        var normalizedPrefix = options.KeyPrefix.Trim('/');
        var expectedPrefix = $"{normalizedPrefix}/{listingId}/";
        if (!storageKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        return storageKey.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
               || storageKey.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
               || storageKey.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
    }

    private static ListingResponse ToDetailResponse(
        Listing listing, string ownerDisplayName, IReadOnlyList<ListingImageResponse> images) =>
        new(listing.Id,
            listing.OwnerUserId,
            ownerDisplayName,
            listing.Title,
            listing.Description,
            listing.Category,
            listing.Size,
            listing.AgeRange,
            listing.Condition.ToString(),
            listing.ContactPreference.ToString(),
            listing.Status.ToString(),
            images,
            listing.CreatedAt,
            listing.UpdatedAt);
}
