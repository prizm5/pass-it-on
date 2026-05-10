using Microsoft.EntityFrameworkCore;
using PassItOn.Api.Domain.Entities;

namespace PassItOn.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthIdentity> AuthIdentities => Set<AuthIdentity>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<ListingReport> ListingReports => Set<ListingReport>();
    public DbSet<BulletinPost> BulletinPosts => Set<BulletinPost>();
    public DbSet<ContentPage> ContentPages => Set<ContentPage>();
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();
    public DbSet<AdminAudit> AdminAudits => Set<AdminAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        ConfigureListings(modelBuilder);
        ConfigureContent(modelBuilder);
        ConfigureAnalytics(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).HasMaxLength(320).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(u => u.PreferredName).HasMaxLength(100);
            e.Property(u => u.Phone).HasMaxLength(30);
            e.Property(u => u.AvatarUrl).HasMaxLength(2048);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(50);
            e.Property(u => u.Status).HasConversion<string>().HasMaxLength(50);
        });

        b.Entity<AuthIdentity>(e =>
        {
            e.HasKey(ai => ai.Id);
            e.Property(ai => ai.Provider).HasConversion<string>().HasMaxLength(50);
            e.Property(ai => ai.ProviderSubject).HasMaxLength(256).IsRequired();
            e.Property(ai => ai.EmailAtProvider).HasMaxLength(320);
            e.HasIndex(ai => new { ai.Provider, ai.ProviderSubject }).IsUnique();
            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(ai => ai.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RefreshSession>(e =>
        {
            e.HasKey(rs => rs.Id);
            e.Property(rs => rs.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(rs => rs.TokenHash).IsUnique();
            e.HasIndex(rs => rs.UserId);
            e.Property(rs => rs.UserAgent).HasMaxLength(512);
            e.Property(rs => rs.IpAddress).HasMaxLength(45);
            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(rs => rs.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureListings(ModelBuilder b)
    {
        b.Entity<Listing>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Title).HasMaxLength(200).IsRequired();
            e.Property(l => l.Description).HasMaxLength(4000).IsRequired();
            e.Property(l => l.Category).HasMaxLength(100).IsRequired();
            e.Property(l => l.Size).HasMaxLength(50);
            e.Property(l => l.AgeRange).HasMaxLength(50);
            e.Property(l => l.Condition).HasConversion<string>().HasMaxLength(50);
            e.Property(l => l.ContactPreference).HasConversion<string>().HasMaxLength(50);
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(50);
            e.HasIndex(l => new { l.Status, l.Category, l.CreatedAt });
            e.HasIndex(l => l.OwnerUserId);
            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(l => l.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ListingImage>(e =>
        {
            e.HasKey(li => li.Id);
            e.Property(li => li.StorageKey).HasMaxLength(1024).IsRequired();
            e.Property(li => li.PublicUrl).HasMaxLength(2048).IsRequired();
            e.HasIndex(li => li.ListingId);
            e.HasOne<Listing>()
                .WithMany()
                .HasForeignKey(li => li.ListingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ListingReport>(e =>
        {
            e.HasKey(lr => lr.Id);
            e.Property(lr => lr.ReasonCode).HasConversion<string>().HasMaxLength(50);
            e.Property(lr => lr.Status).HasConversion<string>().HasMaxLength(50);
            e.Property(lr => lr.Description).HasMaxLength(1000);
            e.HasIndex(lr => lr.ListingId);
            e.HasOne<Listing>()
                .WithMany()
                .HasForeignKey(lr => lr.ListingId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(lr => lr.ReporterUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureContent(ModelBuilder b)
    {
        b.Entity<BulletinPost>(e =>
        {
            e.HasKey(bp => bp.Id);
            e.Property(bp => bp.Title).HasMaxLength(300).IsRequired();
            e.Property(bp => bp.Body).IsRequired();
            e.Property(bp => bp.Status).HasConversion<string>().HasMaxLength(50);
            e.HasIndex(bp => new { bp.Status, bp.PublishAt });
        });

        b.Entity<ContentPage>(e =>
        {
            e.HasKey(cp => cp.Id);
            e.Property(cp => cp.Slug).HasMaxLength(200).IsRequired();
            e.HasIndex(cp => cp.Slug).IsUnique();
            e.Property(cp => cp.Title).HasMaxLength(300).IsRequired();
            e.Property(cp => cp.Body).IsRequired();
            e.Property(cp => cp.ContentType).HasConversion<string>().HasMaxLength(50);
            e.Property(cp => cp.Status).HasConversion<string>().HasMaxLength(50);
        });
    }

    private static void ConfigureAnalytics(ModelBuilder b)
    {
        b.Entity<AnalyticsEvent>(e =>
        {
            e.HasKey(ae => ae.Id);
            e.Property(ae => ae.EventName).HasMaxLength(100).IsRequired();
            e.Property(ae => ae.SubjectType).HasMaxLength(100).IsRequired();
            e.Property(ae => ae.SubjectId).HasMaxLength(256);
            e.Property(ae => ae.Properties).HasColumnType("jsonb");
            e.HasIndex(ae => ae.OccurredAt);
            e.HasIndex(ae => ae.EventName);
        });

        b.Entity<AdminAudit>(e =>
        {
            e.HasKey(aa => aa.Id);
            e.Property(aa => aa.Action).HasConversion<string>().HasMaxLength(100);
            e.Property(aa => aa.TargetType).HasMaxLength(100).IsRequired();
            e.Property(aa => aa.TargetId).HasMaxLength(256).IsRequired();
            e.Property(aa => aa.Reason).HasMaxLength(1000);
            e.Property(aa => aa.Metadata).HasColumnType("jsonb");
            e.HasIndex(aa => aa.AdminUserId);
            e.HasIndex(aa => aa.CreatedAt);
        });
    }
}
