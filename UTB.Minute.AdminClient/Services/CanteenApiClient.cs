using System.Net.Http.Json;
using UTB.Minute.Contracts;

namespace UTB.Minute.AdminClient.Services;

public sealed class CanteenApiClient(HttpClient httpClient, KeycloakTokenProvider tokenProvider, IConfiguration configuration)
{
    public async Task<List<DishDto>> GetDishesAsync()
    {
        await AddAuthorizationAsync();
        return await httpClient.GetFromJsonAsync<List<DishDto>>(ApiRoutes.Dishes) ?? [];
    }

    public async Task<List<MenuItemDto>> GetMenuAsync()
    {
        await AddAuthorizationAsync();
        return await httpClient.GetFromJsonAsync<List<MenuItemDto>>(ApiRoutes.Menu) ?? [];
    }

    public async Task CreateDishAsync(CreateDishRequest request) =>
        await EnsureSuccessAsync(await SendAsync(() => httpClient.PostAsJsonAsync(ApiRoutes.Dishes, request)));

    public async Task UpdateDishAsync(Guid id, UpdateDishRequest request) =>
        await EnsureSuccessAsync(await SendAsync(() => httpClient.PutAsJsonAsync($"{ApiRoutes.Dishes}/{id}", request)));

    public async Task CreateMenuItemAsync(CreateMenuItemRequest request) =>
        await EnsureSuccessAsync(await SendAsync(() => httpClient.PostAsJsonAsync(ApiRoutes.Menu, request)));

    public async Task UpdateMenuItemAsync(Guid id, UpdateMenuItemRequest request) =>
        await EnsureSuccessAsync(await SendAsync(() => httpClient.PutAsJsonAsync($"{ApiRoutes.Menu}/{id}", request)));

    public async Task DeleteMenuItemAsync(Guid id) =>
        await EnsureSuccessAsync(await SendAsync(() => httpClient.DeleteAsync($"{ApiRoutes.Menu}/{id}")));

    private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        await AddAuthorizationAsync();
        return await send();
    }

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
            httpClient.DefaultRequestHeaders.Add("X-Debug-Role", "Manager");
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
