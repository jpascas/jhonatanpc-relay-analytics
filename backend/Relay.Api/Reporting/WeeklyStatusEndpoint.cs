using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Relay.Api.Data;
using Relay.Api.Time;

namespace Relay.Api.Reporting;

public static class WeeklyStatusEndpoint
{
    /// <summary>
    /// <c>GET /api/accounts/{id}/weekly-status[?week=YYYY-MM-DD]</c> (§12). 404 for an unknown account (D18),
    /// 400 for an invalid week (D29) or a non-integer id (model binding).
    /// </summary>
    public static IEndpointRouteBuilder MapWeeklyStatus(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/accounts/{id}/weekly-status", HandleAsync).WithName("GetWeeklyStatus");
        return app;
    }

    private static async Task<IResult> HandleAsync(
        int id,
        string? week,
        RelayDbContext db,
        [FromKeyedServices(ReportingClockServiceCollectionExtensions.Key)] TimeProvider clock,
        IOptions<StatusThresholds> thresholds,
        CancellationToken cancellationToken)
    {
        var account = await db.Accounts
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Name,
                a.Timezone,
                FirstEventUtc = db.ActivityEvents.Where(e => e.AccountId == a.Id).Min(e => (DateTime?)e.OccurredAt),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Account not found", detail: $"Account {id} does not exist.");
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(account.Timezone);
        var planned = WeekPlanner.Plan(week, account.FirstEventUtc, clock.GetUtcNow(), zone, thresholds.Value.BaselineWeeks);
        if (planned.Plan is not { } plan)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid week", detail: planned.Error);
        }

        var rows = await WeeklyCountsQuery.RunAsync(
            db, id, [.. plan.Baseline, plan.Reported], plan.Reported.EndUtc, cancellationToken);

        return Results.Ok(WeeklyStatusAssembler.Assemble(
            id, account.Name, account.Timezone, plan, rows, thresholds.Value));
    }
}
