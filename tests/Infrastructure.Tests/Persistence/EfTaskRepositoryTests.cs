using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence;

namespace Infrastructure.Tests.Persistence;

public class EfTaskRepositoryTests : IDisposable
{
    private readonly SqliteTestDb _db = new();
    private readonly User _alice = new() { Id = Guid.NewGuid(), Email = "alice@taskapp.com", PasswordHash = "h" };
    private readonly User _bob = new() { Id = Guid.NewGuid(), Email = "bob@taskapp.com", PasswordHash = "h" };

    public EfTaskRepositoryTests()
    {
        using var ctx = _db.NewContext();
        ctx.Users.AddRange(_alice, _bob);
        ctx.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Add_then_GetById_round_trips_all_fields()
    {
        var task = NewTask(_alice.Id, TaskItemStatus.InProgress);

        await using (var ctx = _db.NewContext())
            await new EfTaskRepository(ctx).AddAsync(task);

        await using var read = _db.NewContext();
        var loaded = await new EfTaskRepository(read).GetByIdAsync(task.Id);
        loaded.Should().BeEquivalentTo(task);
    }

    [Fact]
    public async Task GetById_returns_null_when_missing()
    {
        await using var ctx = _db.NewContext();

        var loaded = await new EfTaskRepository(ctx).GetByIdAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task GetByUser_returns_only_that_users_tasks_ordered_by_due_date()
    {
        var later = NewTask(_alice.Id, TaskItemStatus.Todo, daysFromNow: 5);
        var sooner = NewTask(_alice.Id, TaskItemStatus.Done, daysFromNow: 1);
        var bobs = NewTask(_bob.Id, TaskItemStatus.Todo);
        await SeedAsync(later, sooner, bobs);

        await using var ctx = _db.NewContext();
        var result = await new EfTaskRepository(ctx).GetByUserAsync(_alice.Id, status: null);

        result.Select(t => t.Id).Should().Equal(sooner.Id, later.Id);
    }

    [Fact]
    public async Task GetByUser_applies_status_filter()
    {
        var todo = NewTask(_alice.Id, TaskItemStatus.Todo);
        var done = NewTask(_alice.Id, TaskItemStatus.Done);
        await SeedAsync(todo, done);

        await using var ctx = _db.NewContext();
        var result = await new EfTaskRepository(ctx).GetByUserAsync(_alice.Id, TaskItemStatus.Done);

        result.Should().ContainSingle().Which.Id.Should().Be(done.Id);
    }

    [Fact]
    public async Task Update_persists_changes()
    {
        var task = NewTask(_alice.Id, TaskItemStatus.Todo);
        await SeedAsync(task);

        await using (var ctx = _db.NewContext())
        {
            var repo = new EfTaskRepository(ctx);
            var tracked = await repo.GetByIdAsync(task.Id);
            tracked!.Title = "Changed";
            tracked.Status = TaskItemStatus.Done;
            await repo.UpdateAsync(tracked);
        }

        await using var read = _db.NewContext();
        var loaded = await new EfTaskRepository(read).GetByIdAsync(task.Id);
        loaded!.Title.Should().Be("Changed");
        loaded.Status.Should().Be(TaskItemStatus.Done);
    }

    [Fact]
    public async Task Delete_removes_task()
    {
        var task = NewTask(_alice.Id, TaskItemStatus.Todo);
        await SeedAsync(task);

        await using (var ctx = _db.NewContext())
        {
            var repo = new EfTaskRepository(ctx);
            await repo.DeleteAsync((await repo.GetByIdAsync(task.Id))!);
        }

        await using var read = _db.NewContext();
        (await new EfTaskRepository(read).GetByIdAsync(task.Id)).Should().BeNull();
    }

    private static TaskItem NewTask(Guid userId, TaskItemStatus status, int daysFromNow = 2) => new()
    {
        Id = Guid.NewGuid(),
        Title = $"Task {status}",
        Description = "desc",
        Status = status,
        DueDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(daysFromNow),
        UserId = userId
    };

    private async Task SeedAsync(params TaskItem[] tasks)
    {
        await using var ctx = _db.NewContext();
        ctx.Tasks.AddRange(tasks);
        await ctx.SaveChangesAsync();
    }
}
