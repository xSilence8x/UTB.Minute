using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UTB.Minute.Db.Migrations;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MinuteDbContext>
{
    public MinuteDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MinuteDbContext>();
        
        // Use a temporary connection string for migrations
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=minute;Username=minute;Password=minute");

        return new MinuteDbContext(optionsBuilder.Options);
    }
}
