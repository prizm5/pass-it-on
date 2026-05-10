using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PassItOn.Api.Auth;
using PassItOn.Api.Domain.Entities;
using PassItOn.Api.Domain.Enums;

namespace PassItOn.Api.Persistence;

public static class DevelopmentBootstrapper
{
    public static async Task InitializeAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var seedOptions = services.GetRequiredService<IOptions<DevAdminSeedOptions>>().Value;

        await db.Database.MigrateAsync();

        if (!seedOptions.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(seedOptions.Email) || string.IsNullOrWhiteSpace(seedOptions.Password))
        {
            app.Logger.LogWarning("Admin seed is enabled but email/password are missing. Skipping seed.");
            return;
        }

        var email = seedOptions.Email.Trim().ToLowerInvariant();
        var displayName = string.IsNullOrWhiteSpace(seedOptions.DisplayName)
            ? "Pass It On Admin"
            : seedOptions.DisplayName.Trim();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            user = new User
            {
                Email = email,
                DisplayName = displayName,
                Role = UserRole.Admin,
                Status = UserStatus.Active,
            };
            db.Users.Add(user);
        }
        else
        {
            user.DisplayName = displayName;
            user.Role = UserRole.Admin;
            user.Status = UserStatus.Active;
            user.DeletedAt = null;
            user.DeletionRequestedAt = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var existingLocalIdentity = await db.AuthIdentities
            .FirstOrDefaultAsync(ai => ai.UserId == user.Id && ai.Provider == AuthProvider.Local);

        if (existingLocalIdentity is not null)
        {
            db.AuthIdentities.Remove(existingLocalIdentity);
        }

        db.AuthIdentities.Add(new AuthIdentity
        {
            UserId = user.Id,
            Provider = AuthProvider.Local,
            ProviderSubject = email,
            EmailAtProvider = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedOptions.Password),
        });

        await db.SaveChangesAsync();

        app.Logger.LogInformation("Development admin ensured for {AdminEmail}", email);
    }
}
