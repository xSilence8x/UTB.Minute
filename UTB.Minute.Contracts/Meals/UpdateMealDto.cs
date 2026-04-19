namespace UTB.Minute.Contracts.Meals;

public record UpdateMealDto(
    string Name,
    string Description,
    decimal Price,
    bool IsActive);