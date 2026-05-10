namespace PassItOn.Api.Images;

public sealed class S3ImageStorageOptions
{
    public const string Section = "ImageStorage";

    public bool Enabled { get; init; }
    public string BucketName { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string PublicBaseUrl { get; init; } = string.Empty;
    public string KeyPrefix { get; init; } = "listings";
    public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;
    public int MaxImagesPerListing { get; init; } = 8;
    public int PresignedUrlMinutes { get; init; } = 10;
}
