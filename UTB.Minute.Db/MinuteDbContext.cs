using Microsoft.EntityFrameworkCore;
using UTB.Minute.Db.Entities;

namespace UTB.Minute.Db;

public class MinuteDbContext : DbContext
{
    public MinuteDbContext(DbContextOptions<MinuteDbContext> options) : base(options)
    {
    }

    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Meal>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Price).HasColumnType("decimal(10,2)");
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Meal)
                .WithMany(x => x.MenuItems)
                .HasForeignKey(x => x.MealId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.MenuItem)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}