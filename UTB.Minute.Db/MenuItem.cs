namespace UTB.Minute.Db;

public sealed class MenuItem
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public Guid DishId { get; set; }
    public Dish? Dish { get; set; }
    public int AvailablePortions { get; set; }

    public int Version { get; set; } = 1;

    public List<Order> Orders { get; set; } = [];
}
