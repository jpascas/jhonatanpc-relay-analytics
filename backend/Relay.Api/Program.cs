using Microsoft.EntityFrameworkCore;
using Relay.Api.Data;
using Relay.Api.Time;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<RelayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Relay")
        ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Relay' is not set.")));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.AddHealthChecks();

// D1: one fixed "now" for the app's lifetime, read once from config or the data; keyed, so only reporting sees it.
builder.Services.AddReportingClock();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// D28: outside Development nothing is migrated or seeded at startup; use `dotnet ef database update`.
if (app.Environment.IsDevelopment())
{
    await DevelopmentSeeder.RunAsync(app);
}

// Resolve the clock at startup (after seeding) so a bad Clock:NowUtc fails fast and "now" is logged.
app.Logger.LogInformation("Clock: now = {NowUtc:O}",
    app.Services.GetRequiredKeyedService<TimeProvider>(ReportingClockServiceCollectionExtensions.Key).GetUtcNow());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/api/health");

app.Run();
