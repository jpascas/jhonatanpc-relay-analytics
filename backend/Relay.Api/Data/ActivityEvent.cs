namespace Relay.Api.Data;

/// <summary>Row of <c>activity_events</c> (schema.sql). Reads for reporting go through the
/// <c>activity_events_dedup</c> view (D8), not this table.</summary>
public class ActivityEvent
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Location { get; set; } = "";
    public string EventType { get; set; } = "";
    /// <summary>UTC.</summary>
    public DateTime OccurredAt { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Outcome { get; set; }
}
