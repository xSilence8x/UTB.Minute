using UTB.Minute.Contracts;
using UTB.Minute.Db;

namespace UTB.Minute.WebApi;

public static class Mapping
{
    public static DishDto ToDto(this Dish dish) =>
        new(dish.Id, dish.Name, dish.Description, dish.Price, dish.IsActive);

    public static MenuItemDto ToDto(this MenuItem menuItem) =>
        new(
            menuItem.Id,
            menuItem.Date,
            menuItem.DishId,
            menuItem.Dish?.Name ?? string.Empty,
            menuItem.Dish?.Description ?? string.Empty,
            menuItem.Dish?.Price ?? 0,
            menuItem.AvailablePortions,
            menuItem.Version);

    public static OrderDto ToDto(this Order order) =>
        new(
            order.Id,
            order.Id.ToString("N")[..8].ToUpperInvariant(),
            order.MenuItemId,
            order.MenuItem?.Date ?? default,
            order.MenuItem?.Dish?.Name ?? string.Empty,
            order.MenuItem?.Dish?.Price ?? 0,
            order.Status.ToDto(),
            order.CreatedAt,
            order.UpdatedAt);

    public static OrderStatusDto ToDto(this OrderStatus status) => status switch
    {
        OrderStatus.Preparing => OrderStatusDto.Preparing,
        OrderStatus.Ready => OrderStatusDto.Ready,
        OrderStatus.Cancelled => OrderStatusDto.Cancelled,
        OrderStatus.Completed => OrderStatusDto.Completed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static OrderStatus ToEntity(this OrderStatusDto status) => status switch
    {
        OrderStatusDto.Preparing => OrderStatus.Preparing,
        OrderStatusDto.Ready => OrderStatus.Ready,
        OrderStatusDto.Cancelled => OrderStatus.Cancelled,
        OrderStatusDto.Completed => OrderStatus.Completed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
