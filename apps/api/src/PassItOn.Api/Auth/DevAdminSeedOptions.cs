namespace PassItOn.Api.Auth;

public sealed class DevAdminSeedOptions
{
    public const string Section = "AdminSeed";

    public bool Enabled { get; init; } = true;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string DisplayName { get; init; } = "Pass It On Admin";
}
