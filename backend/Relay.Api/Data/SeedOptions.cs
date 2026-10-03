namespace Relay.Api.Data;

/// <summary>Config section <c>Seed</c> (D28). Used only in Development.</summary>
public class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Path to seed.sql. Relative paths are resolved against the content root;
    /// the default points from <c>backend/Relay.Api</c> to the repo root.</summary>
    public string Path { get; set; } = "../../seed.sql";

    // Path.Combine returns the second argument unchanged when it is absolute.
    public string ResolvePath(string contentRoot) =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(contentRoot, Path));
}
