namespace UTB.Minute.Db;

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Preparing;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
