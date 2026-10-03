using Microsoft.EntityFrameworkCore;

namespace Relay.Api.Data;

/// <summary>
/// Maps schema.sql. On SQL Server, TIMESTAMP means rowversion, so timestamps are datetime2(0)
/// (values have second precision, E-01). Ids are not generated: seed.sql supplies them.
/// </summary>
public class RelayDbContext(DbContextOptions<RelayDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(a => a.Name).HasColumnName("name").HasColumnType("varchar(120)").IsRequired();
            e.Property(a => a.Industry).HasColumnName("industry").HasColumnType("varchar(60)").IsRequired();
            e.Property(a => a.Timezone).HasColumnName("timezone").HasColumnType("varchar(60)").IsRequired();
            e.Property(a => a.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2(0)");
        });

        modelBuilder.Entity<ActivityEvent>(e =>
        {
            e.ToTable("activity_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Location).HasColumnName("location").HasColumnType("varchar(80)").IsRequired();
            e.Property(x => x.EventType).HasColumnName("event_type").HasColumnType("varchar(40)").IsRequired();
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("datetime2(0)");
            e.Property(x => x.DurationSeconds).HasColumnName("duration_seconds");
            e.Property(x => x.Outcome).HasColumnName("outcome").HasColumnType("varchar(40)");

            // schema.sql: REFERENCES accounts(id), no cascade.
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
