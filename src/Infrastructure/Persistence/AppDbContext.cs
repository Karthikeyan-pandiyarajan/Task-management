using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("Users");
            user.HasKey(u => u.Id);
            user.Property(u => u.Email).IsRequired().HasMaxLength(256);
            // Enforced by the database too, so two concurrent registrations can't both succeed.
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
        });

        modelBuilder.Entity<TaskItem>(task =>
        {
            task.ToTable("Tasks");
            task.HasKey(t => t.Id);
            task.Property(t => t.Title).IsRequired().HasMaxLength(100);
            task.Property(t => t.Description).HasMaxLength(2000);
            // Stored as text so the database is readable and reordering the enum can't corrupt data.
            task.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            task.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            // Every list query filters by owner, optionally by status.
            task.HasIndex(t => new { t.UserId, t.Status });
        });
    }
}
