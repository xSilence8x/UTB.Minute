namespace UTB.Minute.Contracts.MenuItems;

public record UpdateMenuItemDto(
    DateOnly Date,
    Guid MealId,
    int AvailablePortions);