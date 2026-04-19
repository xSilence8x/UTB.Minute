namespace UTB.Minute.Contracts.Meals;

public record CreateMealDto(
    string Name,
    string Description,
    decimal Price);