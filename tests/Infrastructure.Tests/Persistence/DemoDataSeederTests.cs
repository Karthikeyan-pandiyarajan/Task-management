using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

public class DemoDataSeederTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly SqliteTestDb _db = new();
    private readonly Pbkdf2PasswordHasher _hasher = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Seeds_demo_user_with_working_password_and_four_mixed_status_tasks()
    {
        await SeedAsync();

        await using var ctx = _db.NewContext();
        var user = await ctx.Users.SingleAsync();
        user.Email.Should().Be(DemoDataSeeder.DemoEmail);
        _hasher.Verify(DemoDataSeeder.DemoPassword, user.PasswordHash).Should().BeTrue();

        var tasks = await ctx.Tasks.Where(t => t.UserId == user.Id).ToListAsync();
        tasks.Should().HaveCount(4);
        tasks.Select(t => t.Status).Distinct().Should().BeEquivalentTo(
            [TaskItemStatus.Todo, TaskItemStatus.InProgress, TaskItemStatus.Done]);
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_data()
    {
        await SeedAsync();
        await SeedAsync();

        await using var ctx = _db.NewContext();
        (await ctx.Users.CountAsync()).Should().Be(1);
        (await ctx.Tasks.CountAsync()).Should().Be(4);
    }

    private async Task SeedAsync()
    {
        await using var ctx = _db.NewContext();
        await new DemoDataSeeder(ctx, _hasher, new FixedTimeProvider(Now)).SeedAsync();
    }
}
