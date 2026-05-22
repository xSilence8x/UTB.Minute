using System.Net.Http.Json;
using UTB.Minute.Contracts;

namespace UTB.Minute.WebApi.Tests;

[Collection("Aspire database collection")]
public sealed class AspirePostgresApiTests(CanteenApiFactory factory)
{
    [Fact]
    public async Task WebApi_runs_against_aspire_postgres()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-Role", "Manager,Student,Kitchen");

        var dishResponse = await client.PostAsJsonAsync(
            ApiRoutes.Dishes,
            new CreateDishRequest("Aspire test meal", "Created through AppHost and PostgreSQL", 149));
        dishResponse.EnsureSuccessStatusCode();
        var dish = await dishResponse.Content.ReadFromJsonAsync<DishDto>();

        var menuResponse = await client.PostAsJsonAsync(
            ApiRoutes.Menu,
            new CreateMenuItemRequest(DateOnly.FromDateTime(DateTime.Today), dish!.Id, 2));
        menuResponse.EnsureSuccessStatusCode();
        var menuItem = await menuResponse.Content.ReadFromJsonAsync<MenuItemDto>();

        var orderResponse = await client.PostAsJsonAsync(ApiRoutes.Orders, new CreateOrderRequest(menuItem!.Id));
        orderResponse.EnsureSuccessStatusCode();
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(OrderStatusDto.Preparing, order!.Status);

        await using var context = factory.CreateContext();
        var persistedOrder = await context.Orders.FindAsync(order.Id);
        Assert.NotNull(persistedOrder);
    }
}
