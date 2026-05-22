using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UTB.Minute.Contracts;
using UTB.Minute.Db;

namespace UTB.Minute.WebApi.Tests;

[Collection("Aspire database collection")]
public sealed class OrderApiTests(CanteenApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient client = CreateAllRolesClient(factory);

    public async Task InitializeAsync() => await factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetOrders_ReturnsExistingOrders()
    {
        var firstOrder = await CreateOrderInDatabaseAsync(OrderStatus.Preparing);
        var secondOrder = await CreateOrderInDatabaseAsync(OrderStatus.Ready);

        var response = await client.GetAsync($"{ApiRoutes.Orders}?includeCompleted=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();

        Assert.NotNull(orders);
        Assert.Contains(orders, order => order.Id == firstOrder);
        Assert.Contains(orders, order => order.Id == secondOrder);
    }

    [Fact]
    public async Task GetActiveOrders_ExcludesCompletedOrders()
    {
        var activeOrder = await CreateOrderInDatabaseAsync(OrderStatus.Preparing);
        var completedOrder = await CreateOrderInDatabaseAsync(OrderStatus.Completed);

        var response = await client.GetAsync($"{ApiRoutes.Orders}?includeCompleted=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();

        Assert.NotNull(orders);
        Assert.Contains(orders, order => order.Id == activeOrder);
        Assert.DoesNotContain(orders, order => order.Id == completedOrder);
    }

    [Fact]
    public async Task GetOrderById_WithValidId_ReturnsOrder()
    {
        var orderId = await CreateOrderInDatabaseAsync(OrderStatus.Preparing);

        var response = await client.GetAsync($"{ApiRoutes.Orders}/{orderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<OrderDto>();

        Assert.NotNull(dto);
        Assert.Equal(orderId, dto.Id);
        Assert.Equal(OrderStatusDto.Preparing, dto.Status);
    }

    [Fact]
    public async Task GetOrderById_WithInvalidId_ReturnsNotFound()
    {
        var response = await client.GetAsync($"{ApiRoutes.Orders}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_ReturnsCreatedWithLocationPersistsOrderAndDecrementsPortions()
    {
        await using var context = factory.CreateContext();
        var menuItem = await context.MenuItems.FirstAsync(item => item.AvailablePortions > 0);
        var originalPortions = menuItem.AvailablePortions;

        var response = await client.PostAsJsonAsync(ApiRoutes.Orders, new CreateOrderRequest(menuItem.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<OrderDto>();

        Assert.NotNull(dto);
        Assert.Equal(menuItem.Id, dto.MenuItemId);
        Assert.Equal(OrderStatusDto.Preparing, dto.Status);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"{ApiRoutes.Orders}/{dto.Id}", response.Headers.Location.ToString());

        await using var verificationContext = factory.CreateContext();
        var order = await verificationContext.Orders.FindAsync(dto.Id);
        var updatedMenuItem = await verificationContext.MenuItems.FindAsync(menuItem.Id);

        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Preparing, order.Status);
        Assert.NotNull(updatedMenuItem);
        Assert.Equal(originalPortions - 1, updatedMenuItem.AvailablePortions);
    }

    [Fact]
    public async Task CreateOrder_WithNonExistentMenuItem_ReturnsNotFound()
    {
        var response = await client.PostAsJsonAsync(ApiRoutes.Orders, new CreateOrderRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithSoldOutMenuItem_ReturnsConflict()
    {
        await using var context = factory.CreateContext();
        var menuItem = await context.MenuItems.FirstAsync();
        menuItem.AvailablePortions = 0;
        await context.SaveChangesAsync();

        var response = await client.PostAsJsonAsync(ApiRoutes.Orders, new CreateOrderRequest(menuItem.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithValidTransition_ReturnsUpdatedDtoAndPersistsState()
    {
        var orderId = await CreateOrderInDatabaseAsync(OrderStatus.Preparing);

        var response = await client.PutAsJsonAsync(
            $"{ApiRoutes.Orders}/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatusDto.Ready));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<OrderDto>();

        Assert.NotNull(dto);
        Assert.Equal(OrderStatusDto.Ready, dto.Status);

        await using var context = factory.CreateContext();
        var persisted = await context.Orders.FindAsync(orderId);

        Assert.NotNull(persisted);
        Assert.Equal(OrderStatus.Ready, persisted.Status);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithInvalidId_ReturnsNotFound()
    {
        var response = await client.PutAsJsonAsync(
            $"{ApiRoutes.Orders}/{Guid.NewGuid()}/status",
            new UpdateOrderStatusRequest(OrderStatusDto.Ready));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithInvalidTransition_ReturnsBadRequest()
    {
        var orderId = await CreateOrderInDatabaseAsync(OrderStatus.Completed);

        var response = await client.PutAsJsonAsync(
            $"{ApiRoutes.Orders}/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatusDto.Ready));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OrderStatusTransitions_FollowValidProgression()
    {
        var orderId = await CreateOrderInDatabaseAsync(OrderStatus.Preparing);

        var readyResponse = await client.PutAsJsonAsync(
            $"{ApiRoutes.Orders}/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatusDto.Ready));

        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);

        var completedResponse = await client.PutAsJsonAsync(
            $"{ApiRoutes.Orders}/{orderId}/status",
            new UpdateOrderStatusRequest(OrderStatusDto.Completed));

        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);

        await using var context = factory.CreateContext();
        var order = await context.Orders.FindAsync(orderId);

        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    private async Task<Guid> CreateOrderInDatabaseAsync(OrderStatus status)
    {
        await using var context = factory.CreateContext();
        var menuItem = await context.MenuItems.Include(item => item.Dish).FirstAsync();
        var now = DateTimeOffset.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            MenuItemId = menuItem.Id,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        return order.Id;
    }

    private static HttpClient CreateAllRolesClient(CanteenApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-Role", "Manager,Student,Kitchen");
        return client;
    }
}
