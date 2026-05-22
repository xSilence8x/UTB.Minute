using Microsoft.EntityFrameworkCore;
using UTB.Minute.Db;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<MinuteDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("minute-db");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseSqlite("Data Source=minute-dev.db");
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/commands/reset-database", async (MinuteDbContext db, CancellationToken cancellationToken) =>
{
    await DatabaseSeeder.ResetAsync(db, cancellationToken);
    return Results.Ok(new { message = "Database was reset and seeded." });
});

app.MapPost("/commands/seed-database", async (MinuteDbContext db, CancellationToken cancellationToken) =>
{
    await db.Database.EnsureCreatedAsync(cancellationToken);
    await DatabaseSeeder.SeedAsync(db, cancellationToken);
    return Results.Ok(new { message = "Database was seeded." });
});

app.Run();
