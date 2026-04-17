namespace Quantira.WebAPI.Configuration;

public sealed class HangfireSettings
{
    public const string SectionName = "Hangfire";

    public int WorkerCount { get; set; } = 5;

    public HangfireDashboardSettings Dashboard { get; set; } = new();
}

public sealed class HangfireDashboardSettings
{
    public bool Enabled { get; set; } = true;

    public string Path { get; set; } = "/jobs";

    public bool RequireSsl { get; set; } = true;

    public bool IsReadOnly { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public List<string> AllowedIpNetworks { get; set; } = [];

    public bool HasCredentialsConfigured()
        => !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrWhiteSpace(Password);
}