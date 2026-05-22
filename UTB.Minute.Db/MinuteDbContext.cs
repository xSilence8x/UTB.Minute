using Microsoft.EntityFrameworkCore;

namespace UTB.Minute.Db;

public sealed class MinuteDbContext(DbContextOptions<MinuteDbContext> options) : DbContext(options)
{
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Dish>(entity =>
        {
            entity.Property(dish => dish.Name).HasMaxLength(120).IsRequired();
            entity.Property(dish => dish.Description).HasMaxLength(500).IsRequired();
            entity.Property(dish => dish.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasIndex(menuItem => new { menuItem.Date, menuItem.DishId }).IsUnique();
            entity.Property(menuItem => menuItem.Version).IsConcurrencyToken();
            entity.HasOne(menuItem => menuItem.Dish)
                .WithMany(dish => dish.MenuItems)
                .HasForeignKey(menuItem => menuItem.DishId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(order => order.MenuItem)
                .WithMany(menuItem => menuItem.Orders)
                .HasForeignKey(order => order.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<MenuItem>().Where(entry => entry.State == EntityState.Modified))
        {
            entry.Entity.Version++;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
