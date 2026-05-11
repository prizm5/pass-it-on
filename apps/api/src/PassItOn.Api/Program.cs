using System.Text;
using System.Text.Json.Serialization;
using Amazon;
using Amazon.S3;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PassItOn.Api.Auth;
using PassItOn.Api.Endpoints.Admin;
using PassItOn.Api.Endpoints.Public;
using PassItOn.Api.Images;
using PassItOn.Api.Persistence;

static void PromoteEnvironmentVariable(string sourceName, string targetName)
{
    if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(targetName)))
    {
        return;
    }

    var sourceValue = Environment.GetEnvironmentVariable(sourceName);
    if (string.IsNullOrWhiteSpace(sourceValue))
    {
        return;
    }

    Environment.SetEnvironmentVariable(targetName, sourceValue);
}

Env.TraversePath().Load();

PromoteEnvironmentVariable("ADMIN_SEED_ENABLED", "AdminSeed__Enabled");
PromoteEnvironmentVariable("ADMIN_SEED_EMAIL", "AdminSeed__Email");
PromoteEnvironmentVariable("ADMIN_SEED_PASSWORD", "AdminSeed__Password");
PromoteEnvironmentVariable("ADMIN_SEED_DISPLAY_NAME", "AdminSeed__DisplayName");
PromoteEnvironmentVariable("WEB_PUBLIC_URL", "Frontend__WebUrl");
PromoteEnvironmentVariable("ADMIN_PUBLIC_URL", "Frontend__AdminUrl");
PromoteEnvironmentVariable("IMAGE_STORAGE_ENABLED", "ImageStorage__Enabled");
PromoteEnvironmentVariable("IMAGE_STORAGE_BUCKET_NAME", "ImageStorage__BucketName");
PromoteEnvironmentVariable("IMAGE_STORAGE_REGION", "ImageStorage__Region");
PromoteEnvironmentVariable("IMAGE_STORAGE_PUBLIC_BASE_URL", "ImageStorage__PublicBaseUrl");
PromoteEnvironmentVariable("IMAGE_STORAGE_KEY_PREFIX", "ImageStorage__KeyPrefix");
PromoteEnvironmentVariable("IMAGE_STORAGE_MAX_FILE_SIZE_BYTES", "ImageStorage__MaxFileSizeBytes");
PromoteEnvironmentVariable("IMAGE_STORAGE_MAX_IMAGES_PER_LISTING", "ImageStorage__MaxImagesPerListing");
PromoteEnvironmentVariable("IMAGE_STORAGE_PRESIGNED_URL_MINUTES", "ImageStorage__PresignedUrlMinutes");
PromoteEnvironmentVariable("JWT_KEY", "Jwt__Key");
PromoteEnvironmentVariable("GOOGLE_CLIENT_ID", "OAuth__Google__ClientId");
PromoteEnvironmentVariable("GOOGLE_CLIENT_SECRET", "OAuth__Google__ClientSecret");
PromoteEnvironmentVariable("FACEBOOK_APP_ID", "OAuth__Facebook__AppId");
PromoteEnvironmentVariable("FACEBOOK_APP_SECRET", "OAuth__Facebook__AppSecret");

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")))
{
    var postgresDatabase = Environment.GetEnvironmentVariable("POSTGRES_DB");
    var postgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER");
    var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

    if (!string.IsNullOrWhiteSpace(postgresDatabase) &&
        !string.IsNullOrWhiteSpace(postgresUser) &&
        !string.IsNullOrWhiteSpace(postgresPassword))
    {
        var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
        if (string.IsNullOrWhiteSpace(postgresHost))
        {
            postgresHost = "localhost";
        }

        var postgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT");
        if (string.IsNullOrWhiteSpace(postgresPort))
        {
            postgresPort = "5432";
        }

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Postgres",
            $"Host={postgresHost};Port={postgresPort};Database={postgresDatabase};Username={postgresUser};Password={postgresPassword}");
    }
}

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention());

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var allowedOrigins = builder.Configuration.GetSection("Frontend")
    .GetChildren()
    .Select(child => child.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Cast<string>()
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    // Reverse proxy traffic may originate from container bridge addresses.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── JWT ──────────────────────────────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.Configure<DevAdminSeedOptions>(builder.Configuration.GetSection(DevAdminSeedOptions.Section));
builder.Services.Configure<S3ImageStorageOptions>(builder.Configuration.GetSection(S3ImageStorageOptions.Section));
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<S3ImageStorageOptions>>().Value;
    var regionSystemName = string.IsNullOrWhiteSpace(options.Region) ? "us-east-1" : options.Region;
    var region = RegionEndpoint.GetBySystemName(regionSystemName);
    return new AmazonS3Client(region);
});
builder.Services.AddSingleton<TokenService>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.Section);
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["OAuth:Google:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["OAuth:Google:ClientSecret"] ?? string.Empty;
    })
    .AddFacebook(options =>
    {
        options.AppId = builder.Configuration["OAuth:Facebook:AppId"] ?? string.Empty;
        options.AppSecret = builder.Configuration["OAuth:Facebook:AppSecret"] ?? string.Empty;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole(nameof(PassItOn.Api.Domain.Enums.UserRole.Admin)));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

var app = builder.Build();

await DevelopmentBootstrapper.InitializeAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "pass-it-on-api",
    utcTime = DateTimeOffset.UtcNow,
}));

var api = app.MapGroup("/api");
api.MapPublicAuthEndpoints();
api.MapProfileEndpoints();
api.MapListingEndpoints();
api.MapReportEndpoints();
api.MapContentEndpoints();

var admin = api.MapGroup("/admin").RequireAuthorization("Admin");
admin.MapAdminUserEndpoints();
admin.MapAdminListingEndpoints();
admin.MapAdminReportEndpoints();
admin.MapAdminContentEndpoints();
admin.MapAdminAnalyticsEndpoints();
admin.MapAdminAuditEndpoints();

app.Run();

public partial class Program;
