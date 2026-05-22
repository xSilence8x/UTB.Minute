using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UTB.Minute.Contracts;

namespace UTB.Minute.WebApi.Tests;

[Collection("Aspire database collection")]
public sealed class MenuItemApiTests(CanteenApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = CreateManagerClient(factory);

    public async Task InitializeAsync() => await factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetMenu_ReturnsAllSeededMenuItems()
    {
        var response = await client.GetAsync(ApiRoutes.Menu);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var menuItems = await response.Content.ReadFromJsonAsync<List<MenuItemDto>>();

        Assert.NotNull(menuItems);
        Assert.Equal(4, menuItems.Count);
    }

    [Fact]
    public async Task GetMenu_WithTodayFilter_ReturnsTodaysMenu()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var response = await client.GetAsync($"{ApiRoutes.Menu}?date={today:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var menuItems = await response.Content.ReadFromJsonAsync<List<MenuItemDto>>();

        Assert.NotNull(menuItems);
        Assert.Equal(3, menuItems.Count);
        Assert.All(menuItems, item => Assert.Equal(today, item.Date));
    }

    [Fact]
    public async Task GetMenuItemById_WithValidId_ReturnsMenuItem()
    {
        await using var context = factory.CreateContext();
        var menuItem = await context.MenuItems.Include(item => item.Dish).FirstAsync();

        var response = await client.GetAsync($"{ApiRoutes.Menu}/{menuItem.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<MenuItemDto>();

        Assert.NotNull(dto);
        Assert.Equal(menuItem.Id, dto.Id);
        Assert.Equal(menuItem.DishId, dto.DishId);
        Assert.Equal(menuItem.Dish!.Name, dto.DishName);
        Assert.Equal(menuItem.AvailablePortions, dto.AvailablePortions);
    }

    [Fact]
    public async Task GetMenuItemById_WithInvalidId_ReturnsNotFound()
    {
        var response = await client.GetAsync($"{ApiRoutes.Menu}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateMenuItem_ReturnsCreatedWithLocationAndPersistsMenuItem()
    {
        await using var context = factory.CreateContext();
        var dish = await context.Dishes.FirstAsync(dish => dish.IsActive);
        var request = new CreateMenuItemRequest(DateOnly.FromDateTime(DateTime.Today.AddDays(7)), dish.Id, 20);

        var response = await client.PostAsJsonAsync(ApiRoutes.Menu, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<MenuItemDto>();

        Assert.NotNull(dto);
        Assert.Equal(request.Date, dto.Date);
        Assert.Equal(request.DishId, dto.DishId);
        Assert.Equal(request.AvailablePortions, dto.AvailablePortions);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"{ApiRoutes.Menu}/{dto.Id}", response.Headers.Location.ToString());

        await using var verificationContext = factory.CreateContext();
        var persisted = await verificationContext.MenuItems.FindAsync(dto.Id);

        Assert.NotNull(persisted);
        Assert.Equal(request.Date, persisted.Date);
        Assert.Equal(request.DishId, persisted.DishId);
        Assert.Equal(request.AvailablePortions, persisted.AvailablePortions);
    }

    [Fact]
    public async Task CreateMenuItem_WithNonExistentDish_ReturnsNotFound()
    {
        var request = new CreateMenuItemRequest(DateOnly.FromDateTime(DateTime.Today.AddDays(7)), Guid.NewGuid(), 10);

        var response = await client.PostAsJsonAsync(ApiRoutes.Menu, request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMenuItem_WithValidData_ReturnsUpdatedDtoAndPersistsChanges()
    {
        await using var context = factory.CreateContext();
        var menuItem = await context.MenuItems.FirstAsync();
        var dish = await context.Dishes.FirstAsync(dish => dish.Id != menuItem.DishId && dish.IsActive);
        var request = new UpdateMenuItemRequest(DateOnly.FromDateTime(DateTime.Today.AddDays(9)), dish.Id, 25);

        var response = await client.PutAsJsonAsync($"{ApiRoutes.Menu}/{menuItem.Id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<MenuItemDto>();

        Assert.NotNull(dto);
        Assert.Equal(request.Date, dto.Date);
        Assert.Equal(request.DishId, dto.DishId);
        Assert.Equal(request.AvailablePortions, dto.AvailablePortions);

        await using var verificationContext = factory.CreateContext();
        var persisted = await verificationContext.MenuItems.FindAsync(menuItem.Id);

        Assert.NotNull(persisted);
        Assert.Equal(request.Date, persisted.Date);
        Assert.Equal(request.DishId, persisted.DishId);
        Assert.Equal(request.AvailablePortions, persisted.AvailablePortions);
    }

    [Fact]
    public async Task UpdateMenuItem_WithInvalidId_ReturnsNotFound()
    {
        await using var context = factory.CreateContext();
        var dish = await context.Dishes.FirstAsync(dish => dish.IsActive);
        var request = new UpdateMenuItemRequest(DateOnly.FromDateTime(DateTime.Today), dish.Id, 10);

        var response = await client.PutAsJsonAsync($"{ApiRoutes.Menu}/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMenuItem_WithValidId_ReturnsNoContentAndDeletesMenuItem()
    {
        await using var context = factory.CreateContext();
        var menuItem = await context.MenuItems.FirstAsync(item => !item.Orders.Any());

        var response = await client.DeleteAsync($"{ApiRoutes.Menu}/{menuItem.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verificationContext = factory.CreateContext();
        var deleted = await verificationContext.MenuItems.FindAsync(menuItem.Id);

        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteMenuItem_WithInvalidId_ReturnsNotFound()
    {
        var response = await client.DeleteAsync($"{ApiRoutes.Menu}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpClient CreateManagerClient(CanteenApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-Role", "Manager,Student,Kitchen");
        return client;
    }
}
