namespace Relay.Api.Data;

/// <summary>Row of <c>accounts</c> (schema.sql).</summary>
public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Industry { get; set; } = "";
    /// <summary>IANA timezone, e.g. <c>America/Chicago</c>.</summary>
    public string Timezone { get; set; } = "";
    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
