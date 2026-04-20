using Microsoft.EntityFrameworkCore;
using UTB.Minute.Db.Entities;

namespace UTB.Minute.Db;

public class MinuteDbContext(DbContextOptions<MinuteDbContext> options) : DbContext(options)
{
    public DbSet<Meal> Meals { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Order> Orders { get; set; }
}