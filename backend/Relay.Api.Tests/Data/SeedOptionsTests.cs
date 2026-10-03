using Relay.Api.Data;

namespace Relay.Api.Tests.Data;

public class SeedOptionsTests
{
    private static readonly string ContentRoot =
        Path.Combine(Path.GetTempPath(), "repo", "backend", "Relay.Api");

    [Fact]
    public void Default_path_points_from_the_api_project_to_the_repo_root()
    {
        var resolved = new SeedOptions().ResolvePath(ContentRoot);

        Assert.Equal(Path.Combine(Path.GetTempPath(), "repo", "seed.sql"), resolved);
    }

    [Fact]
    public void Absolute_path_is_used_as_is()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "seed", "seed.sql");

        var resolved = new SeedOptions { Path = absolute }.ResolvePath(ContentRoot);

        Assert.Equal(absolute, resolved);
    }

    [Fact]
    public void Relative_path_is_resolved_against_the_content_root()
    {
        var resolved = new SeedOptions { Path = "data/seed.sql" }.ResolvePath(ContentRoot);

        Assert.Equal(Path.Combine(ContentRoot, "data", "seed.sql"), resolved);
    }
}
