namespace UTB.Minute.Contracts.Orders;

public record OrderDto(
    Guid Id,
    Guid MenuItemId,
    string MealName,
    string StudentName,
    DateTime CreatedAtUtc,
    OrderStateDto State);