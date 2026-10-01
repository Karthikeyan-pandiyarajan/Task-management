using Application.Abstractions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class EfUserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default)
        => db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }
}
