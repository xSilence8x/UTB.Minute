namespace UTB.Minute.Db.Entities;

public class MenuItem
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public Guid MealId { get; set; }
    public int AvailablePortions { get; set; }

    public Meal Meal { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}