using UTB.Minute.Db;

namespace UTB.Minute.WebApi;

public static class OrderRules
{
    public static bool CanMoveTo(OrderStatus current, OrderStatus next) => current switch
    {
        OrderStatus.Preparing => next is OrderStatus.Ready or OrderStatus.Cancelled or OrderStatus.Completed,
        OrderStatus.Ready => next is OrderStatus.Completed,
        OrderStatus.Cancelled => next is OrderStatus.Completed,
        OrderStatus.Completed => false,
        _ => false
    };
}
