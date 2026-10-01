using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Persistence;

public class EfUserRepositoryTests : IDisposable
{
    private readonly SqliteTestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Add_then_GetByEmail_returns_user()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "alice@taskapp.com", PasswordHash = "hash" };

        await using (var ctx = _db.NewContext())
            await new EfUserRepository(ctx).AddAsync(user);

        await using var read = _db.NewContext();
        var loaded = await new EfUserRepository(read).GetByEmailAsync("alice@taskapp.com");
        loaded.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task GetByEmail_returns_null_for_unknown_email()
    {
        await using var ctx = _db.NewContext();

        (await new EfUserRepository(ctx).GetByEmailAsync("nobody@taskapp.com")).Should().BeNull();
    }

    [Fact]
    public async Task Database_rejects_duplicate_email()
    {
        await using (var ctx = _db.NewContext())
            await new EfUserRepository(ctx).AddAsync(new User { Id = Guid.NewGuid(), Email = "dup@taskapp.com", PasswordHash = "h" });

        await using var second = _db.NewContext();
        var act = () => new EfUserRepository(second)
            .AddAsync(new User { Id = Guid.NewGuid(), Email = "dup@taskapp.com", PasswordHash = "h" });

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
