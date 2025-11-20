using Microsoft.EntityFrameworkCore;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.Data.Context;

public class DesktopOrganizerDbContext : DbContext
{
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Rule> Rules { get; set; } = null!;
    public DbSet<FileLog> FileLogs { get; set; } = null!;
    public DbSet<UserPreferences> UserPreferences { get; set; } = null!;

    public DesktopOrganizerDbContext(DbContextOptions<DesktopOrganizerDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Fallback for design-time or if not configured externally
            var dbPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopOrganizer",
                "database.db");
            
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure entities
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasMany(e => e.SubCategories)
                  .WithOne(e => e.Parent)
                  .HasForeignKey(e => e.ParentId);
        });

        modelBuilder.Entity<Rule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<FileLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
        });
    }
}
