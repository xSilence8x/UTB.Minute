using System.Net.Http.Json;
using UTB.Minute.Contracts;

namespace UTB.Minute.CanteenClient.Services;

public sealed class CanteenApiClient(HttpClient httpClient, KeycloakTokenProvider tokenProvider, RoleState roleState, IConfiguration configuration)
{
    public async Task<List<MenuItemDto>> GetTodayMenuAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return await httpClient.GetFromJsonAsync<List<MenuItemDto>>($"{ApiRoutes.Menu}?date={today:yyyy-MM-dd}") ?? [];
    }

    public async Task<List<OrderDto>> GetActiveOrdersAsync()
    {
        await AddAuthorizationAsync();
        return await httpClient.GetFromJsonAsync<List<OrderDto>>($"{ApiRoutes.Orders}?includeCompleted=false") ?? [];
    }

    public async Task<List<OrderDto>> GetStudentOrdersAsync()
    {
        return await httpClient.GetFromJsonAsync<List<OrderDto>>(ApiRoutes.StudentOrders) ?? [];
    }

    public async Task<OrderDto> CreateOrderAsync(Guid menuItemId)
    {
        var response = await httpClient.PostAsJsonAsync(ApiRoutes.Orders, new CreateOrderRequest(menuItemId));
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    public async Task ChangeStatusAsync(Guid orderId, OrderStatusDto status)
    {
        await AddAuthorizationAsync();
        var response = await httpClient.PutAsJsonAsync($"{ApiRoutes.Orders}/{orderId}/status", new UpdateOrderStatusRequest(status));
        await EnsureSuccessAsync(response);
    }

    public async Task<Stream> OpenOrderEventsAsync(CancellationToken cancellationToken) =>
        await httpClient.GetStreamAsync(ApiRoutes.OrderEvents, cancellationToken);

    private async Task AddAuthorizationAsync()
    {
        var token = await tokenProvider.GetAccessTokenAsync();
        httpClient.DefaultRequestHeaders.Authorization = null;
        httpClient.DefaultRequestHeaders.Remove("X-Debug-Role");

        if (!string.IsNullOrWhiteSpace(token))
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        else if (!HasKeycloakAuthority())
        {
            httpClient.DefaultRequestHeaders.Add("X-Debug-Role", roleState.CurrentRole);
        }
    }

    private bool HasKeycloakAuthority() =>
        !string.IsNullOrWhiteSpace(configuration["Keycloak:Authority"]) ||
        !string.IsNullOrWhiteSpace(configuration["services:keycloak:http:0"]) ||
        !string.IsNullOrWhiteSpace(configuration["Services:keycloak:http:0"]);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"{(int)response.StatusCode}: {body}");
        }
    }
}
