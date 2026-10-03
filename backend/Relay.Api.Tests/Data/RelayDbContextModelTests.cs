using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Relay.Api.Data;

namespace Relay.Api.Tests.Data;

/// <summary>
/// The EF model must match schema.sql with the S1 type mapping:
/// TIMESTAMP → datetime2(0), VARCHAR(n) → varchar(n), INTEGER → int, no identity on id.
/// Builds the model only; no database connection is opened.
/// </summary>
public class RelayDbContextModelTests
{
    private static IModel DesignTimeModel()
    {
        var options = new DbContextOptionsBuilder<RelayDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;
        using var context = new RelayDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static ITable Table(string name) =>
        DesignTimeModel().GetRelationalModel().Tables.SingleOrDefault(t => t.Name == name)
        ?? throw new Xunit.Sdk.XunitException($"Table '{name}' is not mapped.");

    [Theory]
    [InlineData("accounts", "id", "int", false)]
    [InlineData("accounts", "name", "varchar(120)", false)]
    [InlineData("accounts", "industry", "varchar(60)", false)]
    [InlineData("accounts", "timezone", "varchar(60)", false)]
    [InlineData("accounts", "created_at", "datetime2(0)", false)]
    [InlineData("activity_events", "id", "int", false)]
    [InlineData("activity_events", "account_id", "int", false)]
    [InlineData("activity_events", "location", "varchar(80)", false)]
    [InlineData("activity_events", "event_type", "varchar(40)", false)]
    [InlineData("activity_events", "occurred_at", "datetime2(0)", false)]
    [InlineData("activity_events", "duration_seconds", "int", true)]
    [InlineData("activity_events", "outcome", "varchar(40)", true)]
    public void Column_matches_schema(string table, string column, string storeType, bool nullable)
    {
        var col = Table(table).FindColumn(column)
                  ?? throw new Xunit.Sdk.XunitException($"Column '{table}.{column}' is not mapped.");

        Assert.Equal(storeType, col.StoreType);
        Assert.Equal(nullable, col.IsNullable);
    }

    [Theory]
    [InlineData("accounts", 5)]
    [InlineData("activity_events", 7)]
    public void Table_has_only_schema_columns(string table, int columnCount)
    {
        Assert.Equal(columnCount, Table(table).Columns.Count());
    }

    [Theory]
    [InlineData(typeof(Account))]
    [InlineData(typeof(ActivityEvent))]
    public void Id_is_not_generated_because_seed_supplies_ids(Type entity)
    {
        var id = DesignTimeModel().FindEntityType(entity)!.FindProperty("Id")!;

        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
    }

    [Fact]
    public void Activity_event_account_id_references_accounts()
    {
        var fk = Assert.Single(Table("activity_events").ForeignKeyConstraints);

        Assert.Equal("accounts", fk.PrincipalTable.Name);
        Assert.Equal(["account_id"], fk.Columns.Select(c => c.Name));
        Assert.Equal(ReferentialAction.NoAction, fk.OnDeleteAction);
    }
}
