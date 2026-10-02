using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Relay.Api.Reporting;

namespace Relay.Api.Data;

/// <summary>
/// D13/D15/D22: weekly counts per location in one round trip. Reads the dedup view (D8); week edges arrive as
/// UTC parameters, so SQL never converts zones. Every location × week pair is returned, with zeros (D3, D11).
/// </summary>
public static class WeeklyCountsQuery
{
    private const string SqlTemplate = """
        SELECT l.location AS Location,
               w.idx AS WeekIndex,
               SUM(CASE WHEN d.event_type = 'call_received' THEN 1 ELSE 0 END) AS Calls,
               SUM(CASE WHEN d.event_type = 'lead_created' THEN 1 ELSE 0 END) AS Leads,
               SUM(CASE WHEN d.event_type = 'appointment_set' THEN 1 ELSE 0 END) AS Appointments,
               COUNT(d.event_type) AS Total,
               SUM(CASE WHEN d.event_type = 'call_received' AND d.outcome IS NOT NULL THEN 1 ELSE 0 END) AS KnownCalls,
               SUM(CASE WHEN d.event_type = 'call_received' AND d.outcome = 'missed' THEN 1 ELSE 0 END) AS MissedCalls,
               SUM(CASE WHEN d.event_type = 'call_received' AND d.outcome IS NULL THEN 1 ELSE 0 END) AS UnknownCalls
        FROM (SELECT DISTINCT location
              FROM activity_events_dedup
              WHERE account_id = @account_id AND occurred_at < @locations_before_utc) AS l
        CROSS JOIN (VALUES {0}) AS w(idx, start_utc, end_utc)
        LEFT JOIN activity_events_dedup AS d
               ON d.account_id = @account_id
              AND d.location = l.location
              AND d.occurred_at >= w.start_utc
              AND d.occurred_at < w.end_utc
        GROUP BY l.location, w.idx
        ORDER BY l.location, w.idx;
        """;

    /// <param name="weeks">Baseline weeks oldest first, then the reported week; <c>WeekIndex</c> is the position here.</param>
    /// <param name="locationsBeforeUtc">D22: only locations with an event before this instant are listed.</param>
    public static async Task<IReadOnlyList<LocationWeekCounts>> RunAsync(
        RelayDbContext db, int accountId, IReadOnlyList<WeekRange> weeks, DateTime locationsBeforeUtc,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            // Only generated parameter names go into the SQL text; every value is a parameter.
            var values = string.Join(", ", weeks.Select((_, i) => $"({i}, @start_{i}, @end_{i})"));
            command.CommandText = string.Format(SqlTemplate, values);
            command.Parameters.Add(new SqlParameter("@account_id", SqlDbType.Int) { Value = accountId });
            command.Parameters.Add(DateTime2("@locations_before_utc", locationsBeforeUtc));
            for (var i = 0; i < weeks.Count; i++)
            {
                command.Parameters.Add(DateTime2($"@start_{i}", weeks[i].StartUtc));
                command.Parameters.Add(DateTime2($"@end_{i}", weeks[i].EndUtc));
            }

            var rows = new List<LocationWeekCounts>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new LocationWeekCounts(
                    reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4),
                    reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8)));
            }
            return rows;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    // datetime2 to match the column type; a plain DateTime parameter would be the older datetime type.
    private static DbParameter DateTime2(string name, DateTime value) =>
        new SqlParameter(name, SqlDbType.DateTime2) { Value = value };
}
