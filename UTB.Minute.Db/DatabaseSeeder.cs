using Microsoft.EntityFrameworkCore;

namespace UTB.Minute.Db;

public static class DatabaseSeeder
{
    public static async Task ResetAsync(MinuteDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.Orders.ExecuteDeleteAsync(cancellationToken);
        await db.MenuItems.ExecuteDeleteAsync(cancellationToken);
        await db.Dishes.ExecuteDeleteAsync(cancellationToken);
        await SeedAsync(db, cancellationToken);
    }

    public static async Task SeedAsync(MinuteDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Orders.ExecuteDeleteAsync(cancellationToken);

        if (!await db.Dishes.AnyAsync(cancellationToken))
        {
            var dishes = new[]
            {
                new Dish { Id = Guid.NewGuid(), Name = "Chicken minute steak", Description = "Grilled chicken, herb butter, fries", Price = 129 },
                new Dish { Id = Guid.NewGuid(), Name = "Fried cheese", Description = "Edam cheese, tartar sauce, boiled potatoes", Price = 115 },
                new Dish { Id = Guid.NewGuid(), Name = "Vegetable risotto", Description = "Seasonal vegetables, parmesan, salad", Price = 105 }
            };

            db.Dishes.AddRange(dishes);
            await db.SaveChangesAsync(cancellationToken);
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (await db.MenuItems.AnyAsync(item => item.Date == today, cancellationToken))
        {
            return;
        }

        var menuDishes = await db.Dishes
            .Where(dish => dish.IsActive)
            .OrderBy(dish => dish.Name)
            .Take(3)
            .ToListAsync(cancellationToken);

        if (menuDishes.Count == 0)
        {
            return;
        }

        var portions = new[] { 12, 8, 0 };
        for (var i = 0; i < menuDishes.Count; i++)
        {
            db.MenuItems.Add(new MenuItem
            {
                Id = Guid.NewGuid(),
                Date = today,
                DishId = menuDishes[i].Id,
                AvailablePortions = portions[i]
            });
        }

        db.MenuItems.Add(new MenuItem
        {
            Id = Guid.NewGuid(),
            Date = today.AddDays(1),
            DishId = menuDishes[0].Id,
            AvailablePortions = 10
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
