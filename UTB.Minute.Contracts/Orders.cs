namespace UTB.Minute.Contracts;

public enum OrderStatusDto
{
    Preparing = 0,
    Ready = 1,
    Cancelled = 2,
    Completed = 3
}

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid MenuItemId,
    DateOnly MenuDate,
    string DishName,
    decimal DishPrice,
    OrderStatusDto Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateOrderRequest(Guid MenuItemId);

public sealed record UpdateOrderStatusRequest(OrderStatusDto Status);

public sealed record OrderChangedEvent(
    string EventType,
    OrderDto Order);
