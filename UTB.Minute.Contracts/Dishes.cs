namespace UTB.Minute.Contracts;

public sealed record DishDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    bool IsActive);

public sealed record CreateDishRequest(
    string Name,
    string Description,
    decimal Price);

public sealed record UpdateDishRequest(
    string Name,
    string Description,
    decimal Price,
    bool IsActive);
