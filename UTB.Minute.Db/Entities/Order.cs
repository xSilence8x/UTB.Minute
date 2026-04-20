namespace UTB.Minute.Db.Entities;

public class Order
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public OrderState State { get; set; } = OrderState.Preparing;
    public string StudentName { get; set; } = string.Empty;

    public MenuItem MenuItem { get; set; } = null!;
}