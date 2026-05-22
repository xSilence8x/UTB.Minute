namespace UTB.Minute.Contracts;

public sealed record MenuItemDto(
    Guid Id,
    DateOnly Date,
    Guid DishId,
    string DishName,
    string DishDescription,
    decimal DishPrice,
    int AvailablePortions,
    int Version);

public sealed record CreateMenuItemRequest(
    DateOnly Date,
    Guid DishId,
    int AvailablePortions);

public sealed record UpdateMenuItemRequest(
    DateOnly Date,
    Guid DishId,
    int AvailablePortions);
