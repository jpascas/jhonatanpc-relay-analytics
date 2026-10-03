namespace Relay.Api.IntegrationTests.Infrastructure;

public static class RepoPaths
{
    /// <summary>The repo's seed.sql (read only, never copied or changed), found by walking up from the test binaries.</summary>
    public static string SeedSql { get; } = FindUpwards("seed.sql");

    private static string FindUpwards(string fileName)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        throw new FileNotFoundException($"'{fileName}' not found above {AppContext.BaseDirectory}.");
    }
}
