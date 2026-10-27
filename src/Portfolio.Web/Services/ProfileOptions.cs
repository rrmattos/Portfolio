namespace Portfolio.Web.Services;

/// <summary>Static personal info shown in the hero / footer (configured in appsettings "Profile").</summary>
public class ProfileOptions
{
    public string Name { get; set; } = "Rafael";
    public string Headline { get; set; } = "Desenvolvedor Unity3d C# .NET";
    public string Bio { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Email { get; set; }
    public string? GitHubUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? AvatarUrl { get; set; } = "/images/eu.png";
    public List<string> Skills { get; set; } = [];

    public string Initials =>
        string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
}
