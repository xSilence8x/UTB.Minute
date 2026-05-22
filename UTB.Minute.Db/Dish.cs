namespace UTB.Minute.Db;

public sealed class Dish
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public List<MenuItem> MenuItems { get; set; } = [];
}
