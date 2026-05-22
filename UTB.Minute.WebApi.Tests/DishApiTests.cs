using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UTB.Minute.Contracts;

namespace UTB.Minute.WebApi.Tests;

[Collection("Aspire database collection")]
public sealed class DishApiTests(CanteenApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = CreateManagerClient(factory);

    public async Task InitializeAsync() => await factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetDishes_ReturnsSeededDishes()
    {
        var response = await client.GetAsync(ApiRoutes.Dishes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dishes = await response.Content.ReadFromJsonAsync<List<DishDto>>();

        Assert.NotNull(dishes);
        Assert.Equal(3, dishes.Count);
        Assert.Contains(dishes, dish => dish.Name == "Chicken minute steak" && dish.IsActive);
        Assert.Contains(dishes, dish => dish.Name == "Vegetable risotto" && dish.IsActive);
    }

    [Fact]
    public async Task GetDishById_WithValidId_ReturnsDish()
    {
        await using var context = factory.CreateContext();
        var dish = await context.Dishes.FirstAsync();

        var response = await client.GetAsync($"{ApiRoutes.Dishes}/{dish.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DishDto>();

        Assert.NotNull(dto);
        Assert.Equal(dish.Id, dto.Id);
        Assert.Equal(dish.Name, dto.Name);
        Assert.Equal(dish.Description, dto.Description);
        Assert.Equal(dish.Price, dto.Price);
        Assert.Equal(dish.IsActive, dto.IsActive);
    }

    [Fact]
    public async Task GetDishById_WithInvalidId_ReturnsNotFound()
    {
        var response = await client.GetAsync($"{ApiRoutes.Dishes}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateDish_ReturnsCreatedWithLocationAndPersistsDish()
    {
        var request = new CreateDishRequest("Grilled salmon", "Fish, lemon butter, potatoes", 189);

        var response = await client.PostAsJsonAsync(ApiRoutes.Dishes, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DishDto>();

        Assert.NotNull(dto);
        Assert.Equal(request.Name, dto.Name);
        Assert.Equal(request.Description, dto.Description);
        Assert.Equal(request.Price, dto.Price);
        Assert.True(dto.IsActive);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"{ApiRoutes.Dishes}/{dto.Id}", response.Headers.Location.ToString());

        await using var context = factory.CreateContext();
        var persisted = await context.Dishes.FindAsync(dto.Id);

        Assert.NotNull(persisted);
        Assert.Equal(request.Name, persisted.Name);
        Assert.Equal(request.Description, persisted.Description);
        Assert.Equal(request.Price, persisted.Price);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task UpdateDish_WithValidData_ReturnsUpdatedDtoAndPersistsChanges()
    {
        await using var context = factory.CreateContext();
        var dish = await context.Dishes.FirstAsync();
        var request = new UpdateDishRequest("Updated dish", "Updated description", 299, false);

        var response = await client.PutAsJsonAsync($"{ApiRoutes.Dishes}/{dish.Id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DishDto>();

        Assert.NotNull(dto);
        Assert.Equal(request.Name, dto.Name);
        Assert.False(dto.IsActive);

        await using var verificationContext = factory.CreateContext();
        var persisted = await verificationContext.Dishes.FindAsync(dish.Id);

        Assert.NotNull(persisted);
        Assert.Equal(request.Name, persisted.Name);
        Assert.Equal(request.Description, persisted.Description);
        Assert.Equal(request.Price, persisted.Price);
        Assert.False(persisted.IsActive);
    }

    [Fact]
    public async Task UpdateDish_WithInvalidId_ReturnsNotFound()
    {
        var request = new UpdateDishRequest("Missing", "Missing", 100, true);

        var response = await client.PutAsJsonAsync($"{ApiRoutes.Dishes}/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateDish_PreservesDishAndMarksItInactive()
    {
        await using var context = factory.CreateContext();
        var dish = await context.Dishes.FirstAsync();

        var response = await client.PutAsJsonAsync(
            $"{ApiRoutes.Dishes}/{dish.Id}",
            new UpdateDishRequest(dish.Name, dish.Description, dish.Price, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verificationContext = factory.CreateContext();
        var persisted = await verificationContext.Dishes.FindAsync(dish.Id);

        Assert.NotNull(persisted);
        Assert.Equal(dish.Name, persisted.Name);
        Assert.False(persisted.IsActive);
    }

    private static HttpClient CreateManagerClient(CanteenApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-Role", "Manager,Student,Kitchen");
        return client;
    }
}
