using Domain.Entities;

namespace Application.Abstractions;

public interface IUserRepository
{
    /// <param name="normalizedEmail">Trimmed, lower-case email.</param>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}
