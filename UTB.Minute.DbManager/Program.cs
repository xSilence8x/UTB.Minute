using Microsoft.EntityFrameworkCore;
using UTB.Minute.Db;
using UTB.Minute.Db.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<MinuteDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("minute-db")
        ?? throw new InvalidOperationException("Connection string 'minute-db' not found.");

    options.UseNpgsql(connectionString);
});

var app = builder.Build();

// Ensure database exists and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MinuteDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapDefaultEndpoints();

app.UseHttpsRedirection();

app.MapPost("/reset-database", ResetDatabase)
    .WithName("ResetDatabase");

app.Run();

static async Task<IResult> ResetDatabase(MinuteDbContext dbContext)
{
    try
    {
        // Ensure DB and tables exist
        await dbContext.Database.MigrateAsync();

        // Delete data in correct order because of foreign keys
        await dbContext.Database.ExecuteSqlRawAsync("""DELETE FROM "Orders";""");
        await dbContext.Database.ExecuteSqlRawAsync("""DELETE FROM "MenuItems";""");
        await dbContext.Database.ExecuteSqlRawAsync("""DELETE FROM "Meals";""");

        await SeedDatabaseAsync(dbContext);

        return Results.Ok(new { message = "Database reset and seeded successfully." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}

static async Task SeedDatabaseAsync(MinuteDbContext dbContext)
{
    if (await dbContext.Meals.AnyAsync())
    {
        return;
    }

    var meals = new[]
    {
        new Meal
        {
            Id = Guid.NewGuid(),
            Name = "Chicken schnitzel",
            Description = "Crispy chicken schnitzel with potatoes",
            Price = 120,
            IsActive = true
        },
        new Meal
        {
            Id = Guid.NewGuid(),
            Name = "Pork goulash",
            Description = "Traditional goulash with pasta",
            Price = 140,
            IsActive = true
        },
        new Meal
        {
            Id = Guid.NewGuid(),
            Name = "Fish fillet",
            Description = "Baked fish fillet with spinach",
            Price = 150,
            IsActive = true
        },
        new Meal
        {
            Id = Guid.NewGuid(),
            Name = "Vegetarian burger",
            Description = "Homemade burger with herb cheese",
            Price = 110,
            IsActive = true
        }
    };

    await dbContext.Meals.AddRangeAsync(meals);
    await dbContext.SaveChangesAsync();

    var today = DateOnly.FromDateTime(DateTime.Today);

    var menuItems = meals.Select((meal, index) => new MenuItem
    {
        Id = Guid.NewGuid(),
        MealId = meal.Id,
        Date = today,
        AvailablePortions = 10 + (index * 5)
    }).ToList();

    await dbContext.MenuItems.AddRangeAsync(menuItems);
    await dbContext.SaveChangesAsync();
}