using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using UTB.Minute.Contracts;
using UTB.Minute.Db;
using UTB.Minute.WebApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<OrderEventStream>();
builder.Services.AddHttpClient();
builder.Services.Configure<KeycloakAuthenticationOptions>(builder.Configuration.GetSection("Keycloak"));
builder.Services.PostConfigure<KeycloakAuthenticationOptions>(options =>
{
    options.AllowDebugRoleHeader = builder.Environment.IsDevelopment() ||
        builder.Configuration.GetValue("Keycloak:AllowDebugRoleHeader", false);
});
builder.Services.AddAuthentication(AuthConstants.Scheme)
    .AddScheme<AuthenticationSchemeOptions, KeycloakAuthenticationHandler>(AuthConstants.Scheme, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthConstants.ManagerPolicy, policy => policy.RequireRole("Manager"));
    options.AddPolicy(AuthConstants.KitchenPolicy, policy => policy.RequireRole("Kitchen"));
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

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

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MinuteDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DatabaseSeeder.SeedAsync(db);
}

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapGet(ApiRoutes.Dishes, async (MinuteDbContext db) =>
        await db.Dishes
            .OrderBy(dish => dish.Name)
            .Select(dish => dish.ToDto())
            .ToListAsync())
    .WithName("GetDishes");

app.MapGet($"{ApiRoutes.Dishes}/{{id:guid}}", async Task<Results<Ok<DishDto>, NotFound>> (Guid id, MinuteDbContext db) =>
{
    var dish = await db.Dishes.FindAsync(id);
    return dish is null ? TypedResults.NotFound() : TypedResults.Ok(dish.ToDto());
});

app.MapPost(ApiRoutes.Dishes, async Task<Created<DishDto>> (CreateDishRequest request, MinuteDbContext db) =>
{
    var dish = new Dish
    {
        Id = Guid.NewGuid(),
        Name = request.Name.Trim(),
        Description = request.Description.Trim(),
        Price = request.Price,
        IsActive = true
    };

    db.Dishes.Add(dish);
    await db.SaveChangesAsync();

    return TypedResults.Created($"{ApiRoutes.Dishes}/{dish.Id}", dish.ToDto());
}).RequireAuthorization(AuthConstants.ManagerPolicy);

app.MapPut($"{ApiRoutes.Dishes}/{{id:guid}}", async Task<Results<Ok<DishDto>, NotFound>> (Guid id, UpdateDishRequest request, MinuteDbContext db) =>
{
    var dish = await db.Dishes.FindAsync(id);
    if (dish is null)
    {
        return TypedResults.NotFound();
    }

    dish.Name = request.Name.Trim();
    dish.Description = request.Description.Trim();
    dish.Price = request.Price;
    dish.IsActive = request.IsActive;
    await db.SaveChangesAsync();

    return TypedResults.Ok(dish.ToDto());
}).RequireAuthorization(AuthConstants.ManagerPolicy);

app.MapGet(ApiRoutes.Menu, async (DateOnly? date, MinuteDbContext db) =>
{
    var query = db.MenuItems.Include(menuItem => menuItem.Dish).AsQueryable();
    if (date is not null)
    {
        query = query.Where(menuItem => menuItem.Date == date.Value);
    }

    return await query
        .OrderBy(menuItem => menuItem.Date)
        .ThenBy(menuItem => menuItem.Dish!.Name)
        .Select(menuItem => menuItem.ToDto())
        .ToListAsync();
});

app.MapGet($"{ApiRoutes.Menu}/{{id:guid}}", async Task<Results<Ok<MenuItemDto>, NotFound>> (Guid id, MinuteDbContext db) =>
{
    var menuItem = await db.MenuItems
        .Include(item => item.Dish)
        .FirstOrDefaultAsync(item => item.Id == id);

    return menuItem is null ? TypedResults.NotFound() : TypedResults.Ok(menuItem.ToDto());
});

app.MapPost(ApiRoutes.Menu, async Task<Results<Created<MenuItemDto>, NotFound>> (CreateMenuItemRequest request, MinuteDbContext db) =>
{
    var dishExists = await db.Dishes.AnyAsync(dish => dish.Id == request.DishId && dish.IsActive);
    if (!dishExists)
    {
        return TypedResults.NotFound();
    }

    var menuItem = new MenuItem
    {
        Id = Guid.NewGuid(),
        Date = request.Date,
        DishId = request.DishId,
        AvailablePortions = request.AvailablePortions
    };

    db.MenuItems.Add(menuItem);
    await db.SaveChangesAsync();
    await db.Entry(menuItem).Reference(item => item.Dish).LoadAsync();

    return TypedResults.Created($"{ApiRoutes.Menu}/{menuItem.Id}", menuItem.ToDto());
}).RequireAuthorization(AuthConstants.ManagerPolicy);

app.MapPut($"{ApiRoutes.Menu}/{{id:guid}}", async Task<Results<Ok<MenuItemDto>, NotFound>> (Guid id, UpdateMenuItemRequest request, MinuteDbContext db) =>
{
    var menuItem = await db.MenuItems.Include(item => item.Dish).FirstOrDefaultAsync(item => item.Id == id);
    if (menuItem is null)
    {
        return TypedResults.NotFound();
    }

    var dishExists = await db.Dishes.AnyAsync(dish => dish.Id == request.DishId && dish.IsActive);
    if (!dishExists)
    {
        return TypedResults.NotFound();
    }

    menuItem.Date = request.Date;
    menuItem.DishId = request.DishId;
    menuItem.AvailablePortions = request.AvailablePortions;
    await db.SaveChangesAsync();
    await db.Entry(menuItem).Reference(item => item.Dish).LoadAsync();

    return TypedResults.Ok(menuItem.ToDto());
}).RequireAuthorization(AuthConstants.ManagerPolicy);

app.MapDelete($"{ApiRoutes.Menu}/{{id:guid}}", async Task<Results<NoContent, NotFound>> (Guid id, MinuteDbContext db) =>
{
    var menuItem = await db.MenuItems.FindAsync(id);
    if (menuItem is null)
    {
        return TypedResults.NotFound();
    }

    db.MenuItems.Remove(menuItem);
    await db.SaveChangesAsync();
    return TypedResults.NoContent();
}).RequireAuthorization(AuthConstants.ManagerPolicy);

app.MapGet(ApiRoutes.Orders, async (bool includeCompleted, MinuteDbContext db) =>
{
    var query = db.Orders.Include(order => order.MenuItem)!.ThenInclude(menuItem => menuItem!.Dish).AsQueryable();
    if (!includeCompleted)
    {
        query = query.Where(order => order.Status != OrderStatus.Completed);
    }

    return await query
        .OrderByDescending(order => order.CreatedAt)
        .Select(order => order.ToDto())
        .ToListAsync();
}).RequireAuthorization(AuthConstants.KitchenPolicy);

app.MapGet($"{ApiRoutes.Orders}/{{id:guid}}", async Task<Results<Ok<OrderDto>, NotFound>> (Guid id, MinuteDbContext db) =>
{
    var order = await db.Orders
        .Include(item => item.MenuItem)!.ThenInclude(item => item!.Dish)
        .FirstOrDefaultAsync(item => item.Id == id);

    return order is null ? TypedResults.NotFound() : TypedResults.Ok(order.ToDto());
}).RequireAuthorization(AuthConstants.KitchenPolicy);

app.MapGet(ApiRoutes.StudentOrders, async (MinuteDbContext db) =>
        await db.Orders
            .Include(order => order.MenuItem)!.ThenInclude(menuItem => menuItem!.Dish)
            .OrderByDescending(order => order.CreatedAt)
            .Take(20)
            .Select(order => order.ToDto())
            .ToListAsync())
    .AllowAnonymous();

app.MapPost(ApiRoutes.Orders, async Task<Results<Created<OrderDto>, NotFound, Conflict<string>>> (CreateOrderRequest request, MinuteDbContext db, OrderEventStream events) =>
{
    await using var transaction = await db.Database.BeginTransactionAsync();
    var menuItem = await db.MenuItems.Include(item => item.Dish).FirstOrDefaultAsync(item => item.Id == request.MenuItemId);
    if (menuItem is null)
    {
        return TypedResults.NotFound();
    }

    if (menuItem.AvailablePortions <= 0)
    {
        return TypedResults.Conflict("The selected meal is sold out.");
    }

    menuItem.AvailablePortions--;
    var now = DateTimeOffset.UtcNow;
    var order = new Order
    {
        Id = Guid.NewGuid(),
        MenuItemId = menuItem.Id,
        Status = OrderStatus.Preparing,
        CreatedAt = now,
        UpdatedAt = now
    };

    db.Orders.Add(order);
    try
    {
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return TypedResults.Conflict("The selected meal was just sold out. Please refresh the menu.");
    }

    await db.Entry(order).Reference(item => item.MenuItem).LoadAsync();
    await db.Entry(order.MenuItem!).Reference(item => item.Dish).LoadAsync();

    var dto = order.ToDto();
    await events.PublishAsync(new OrderChangedEvent("created", dto));

    return TypedResults.Created($"{ApiRoutes.Orders}/{order.Id}", dto);
}).AllowAnonymous();

app.MapPut($"{ApiRoutes.Orders}/{{id:guid}}/status", async Task<Results<Ok<OrderDto>, NotFound, BadRequest<string>>> (Guid id, UpdateOrderStatusRequest request, MinuteDbContext db, OrderEventStream events) =>
{
    var order = await db.Orders.Include(item => item.MenuItem)!.ThenInclude(item => item!.Dish).FirstOrDefaultAsync(item => item.Id == id);
    if (order is null)
    {
        return TypedResults.NotFound();
    }

    var newStatus = request.Status.ToEntity();
    if (!OrderRules.CanMoveTo(order.Status, newStatus))
    {
        return TypedResults.BadRequest($"Order cannot move from {order.Status} to {newStatus}.");
    }

    order.Status = newStatus;
    order.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();

    var dto = order.ToDto();
    await events.PublishAsync(new OrderChangedEvent("statusChanged", dto));

    return TypedResults.Ok(dto);
}).RequireAuthorization(AuthConstants.KitchenPolicy);

app.MapGet(ApiRoutes.OrderEvents, async (HttpContext context, OrderEventStream events) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    await foreach (var orderEvent in events.ReadAllAsync(context.RequestAborted))
    {
        var json = JsonSerializer.Serialize(orderEvent, jsonOptions);
        await context.Response.WriteAsync($"event: order-changed\ndata: {json}\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }
}).AllowAnonymous();

app.Run();

public partial class Program;
