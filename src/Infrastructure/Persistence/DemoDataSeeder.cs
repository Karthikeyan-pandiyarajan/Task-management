using Application.Abstractions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

// Idempotent: both APIs run it on startup and only the first run inserts anything.
public sealed class DemoDataSeeder(AppDbContext db, IPasswordHasher hasher, TimeProvider timeProvider)
{
    public const string DemoEmail = "demo@taskapp.com";
    public const string DemoPassword = "Demo@1234";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == DemoEmail, ct))
            return;

        var user = new User { Id = Guid.NewGuid(), Email = DemoEmail, PasswordHash = hasher.Hash(DemoPassword) };
        var today = timeProvider.GetUtcNow().UtcDateTime.Date;

        db.Users.Add(user);
        db.Tasks.AddRange(
            NewTask(user.Id, "Prepare sprint demo", "Slides and a short live walkthrough.", TaskItemStatus.InProgress, today.AddDays(2)),
            NewTask(user.Id, "Review pull requests", null, TaskItemStatus.Todo, today.AddDays(1)),
            NewTask(user.Id, "Update project README", "Add setup steps and demo credentials.", TaskItemStatus.Todo, today.AddDays(7)),
            NewTask(user.Id, "Set up CI pipeline", "Build and run tests on every push.", TaskItemStatus.Done, today.AddDays(-1)));

        await db.SaveChangesAsync(ct);
    }

    private static TaskItem NewTask(Guid userId, string title, string? description, TaskItemStatus status, DateTime dueDate)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Status = status,
            DueDate = dueDate,
            UserId = userId
        };
}
