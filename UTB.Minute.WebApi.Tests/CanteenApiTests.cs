extern alias AppHost;

using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.EntityFrameworkCore;
using UTB.Minute.Contracts;
using UTB.Minute.Db;

namespace UTB.Minute.WebApi.Tests;

[Collection("Aspire database collection")]
public sealed class CanteenApiTests
{
    private readonly HttpClient client;
    private readonly CanteenApiFactory factory;

    public CanteenApiTests(CanteenApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-Role", "Manager,Student,Kitchen");
    }

    [Fact]
    public async Task Dishes_can_be_created_and_updated()
    {
        var created = await CreateDishAsync("Test pasta");

        var updateResponse = await client.PutAsJsonAsync(
            $"{ApiRoutes.Dishes}/{created.Id}",
            new UpdateDishRequest("Test pasta updated", "Tomato sauce", 133, false));

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<DishDto>();

        Assert.Equal("Test pasta updated", updated!.Name);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Menu_items_can_be_created_updated_and_deleted()
    {
        var dish = await CreateDishAsync("Menu schnitzel");
        var createResponse = await client.PostAsJsonAsync(
            ApiRoutes.Menu,
            new CreateMenuItemRequest(DateOnly.FromDateTime(DateTime.Today), dish.Id, 5));

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItemDto>();
        Assert.Equal(5, created!.AvailablePortions);

        var updateResponse = await client.PutAsJsonAsync(
            $"{ApiRoutes.Menu}/{created.Id}",
            new UpdateMenuItemRequest(DateOnly.FromDateTime(DateTime.Today), dish.Id, 3));
        updateResponse.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"{ApiRoutes.Menu}/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Ordering_decreases_available_portions_and_status_can_change()
    {
        var dish = await CreateDishAsync("Order burger");
        var menuResponse = await client.PostAsJsonAsync(
            ApiRoutes.Menu,
            new CreateMenuItemRequest(DateOnly.FromDateTime(DateTime.Today), dish.Id, 1));
        var menuItem = await menuResponse.Content.ReadFromJsonAsync<MenuItemDto>();

        var orderResponse = await client.PostAsJsonAsync(ApiRoutes.Orders, new CreateOrderRequest(menuItem!.Id));
        orderResponse.EnsureSuccessStatusCode();
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();

        var menu = await client.GetFromJsonAsync<List<MenuItemDto>>($"{ApiRoutes.Menu}?date={DateOnly.FromDateTime(DateTime.Today):yyyy-MM-dd}");
        Assert.Equal(0, menu!.Single(item => item.Id == menuItem.Id).AvailablePortions);

        var statusResponse = await client.PutAsJsonAsync(
            $"{ApiRoutes.Orders}/{order!.Id}/status",
            new UpdateOrderStatusRequest(OrderStatusDto.Ready));
        statusResponse.EnsureSuccessStatusCode();
        var updated = await statusResponse.Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(OrderStatusDto.Ready, updated!.Status);
    }

    [Fact]
    public async Task Anonymous_customer_can_create_order_and_list_public_order_board()
    {
        var dish = await CreateDishAsync("Student order noodles");
        var menuResponse = await client.PostAsJsonAsync(
            ApiRoutes.Menu,
            new CreateMenuItemRequest(DateOnly.FromDateTime(DateTime.Today), dish.Id, 2));
        var menuItem = await menuResponse.Content.ReadFromJsonAsync<MenuItemDto>();

        var customer = factory.CreateClient();
        var orderResponse = await customer.PostAsJsonAsync(ApiRoutes.Orders, new CreateOrderRequest(menuItem!.Id));
        orderResponse.EnsureSuccessStatusCode();
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();

        Assert.False(string.IsNullOrWhiteSpace(order!.OrderNumber));

        var orders = await customer.GetFromJsonAsync<List<OrderDto>>(ApiRoutes.StudentOrders);

        Assert.Contains(orders!, item => item.Id == order.Id && item.OrderNumber == order.OrderNumber);
    }

    [Fact]
    public async Task Concurrent_orders_for_last_portion_return_one_conflict()
    {
        var dish = await CreateDishAsync("Last portion curry");
        var menuResponse = await client.PostAsJsonAsync(
            ApiRoutes.Menu,
            new CreateMenuItemRequest(DateOnly.FromDateTime(DateTime.Today), dish.Id, 1));
        var menuItem = await menuResponse.Content.ReadFromJsonAsync<MenuItemDto>();

        var first = factory.CreateClient();
        var second = factory.CreateClient();
        first.DefaultRequestHeaders.Add("X-Debug-Role", "Student");
        second.DefaultRequestHeaders.Add("X-Debug-Role", "Student");

        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(ApiRoutes.Orders, new CreateOrderRequest(menuItem!.Id)),
            second.PostAsJsonAsync(ApiRoutes.Orders, new CreateOrderRequest(menuItem!.Id)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Protected_endpoints_require_expected_roles()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            ApiRoutes.Dishes,
            new CreateDishRequest("Unauthorized soup", "No role", 50));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_reject_wrong_roles()
    {
        var student = factory.CreateClient();
        student.DefaultRequestHeaders.Add("X-Debug-Role", "Student");

        var response = await student.PostAsJsonAsync(
            ApiRoutes.Dishes,
            new CreateDishRequest("Forbidden soup", "Wrong role", 50));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<DishDto> CreateDishAsync(string name)
    {
        var response = await client.PostAsJsonAsync(
            ApiRoutes.Dishes,
            new CreateDishRequest(name, "Created by integration test", 123));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DishDto>())!;
    }
}

public sealed class CanteenApiFactory : IAsyncLifetime
{
    private DistributedApplication app = null!;
    private string connectionString = string.Empty;

    public HttpClient CreateClient()
    {
        var client = app.CreateHttpClient("webapi", "https");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    public async Task InitializeAsync()
    {
        var cancellationToken = CancellationToken.None;
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<AppHost::Program>(
            ["--environment=Testing"],
            cancellationToken);

        app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        await app.ResourceNotifications.WaitForResourceHealthyAsync("minute-db", cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webapi", cancellationToken);

        connectionString = await app.GetConnectionStringAsync("minute-db", cancellationToken)
            ?? throw new InvalidOperationException("The test database connection string was not available.");

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        await ResetDatabaseAsync(cancellationToken);
        await WaitForWebApiAsync(cancellationToken);
    }

    public async Task DisposeAsync()
    {
        await app.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public MinuteDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MinuteDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new MinuteDbContext(options);
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();

        await context.Database.EnsureCreatedAsync(cancellationToken);
        await context.Orders.ExecuteDeleteAsync(cancellationToken);
        await context.MenuItems.ExecuteDeleteAsync(cancellationToken);
        await context.Dishes.ExecuteDeleteAsync(cancellationToken);

        await DatabaseSeeder.SeedAsync(context, cancellationToken);
    }

    private async Task WaitForWebApiAsync(CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        Exception? lastException = null;
        while (!linked.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync(ApiRoutes.Dishes, linked.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastException = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), linked.Token);
        }

        throw new TimeoutException("The Web API did not respond to a readiness probe within 60 seconds.", lastException);
    }
}
