using Microsoft.EntityFrameworkCore;
using TestApi.Models;

public class AppDbContext : DbContext
{
    public DbSet<ValueRecord> Values { get; set; }
    public DbSet<ResultRecord> Results { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ValueRecord>(e => 
        {   e.HasIndex(x => new { x.FileName, x.Date }); 
            e.Property(x => x.FileName).IsRequired(); 
        });

        modelBuilder.Entity<ResultRecord>()
            .HasIndex(x => x.FileName)
            .IsUnique();
    }
}