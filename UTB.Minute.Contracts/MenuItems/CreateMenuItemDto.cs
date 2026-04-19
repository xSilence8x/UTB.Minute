namespace UTB.Minute.Contracts.MenuItems;

public record CreateMenuItemDto(
    DateOnly Date,
    Guid MealId,
    int AvailablePortions);