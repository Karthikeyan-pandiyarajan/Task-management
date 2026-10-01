using Application.Abstractions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class EfTaskRepository(AppDbContext db) : ITaskRepository
{
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<TaskItem>> GetByUserAsync(Guid userId, TaskItemStatus? status, CancellationToken ct = default)
    {
        var query = db.Tasks.AsNoTracking().Where(t => t.UserId == userId);
        if (status is not null)
            query = query.Where(t => t.Status == status);

        return await query.OrderBy(t => t.DueDate).ThenBy(t => t.Title).ToListAsync(ct);
    }

    public async Task AddAsync(TaskItem task, CancellationToken ct = default)
    {
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken ct = default)
    {
        db.Tasks.Update(task);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(TaskItem task, CancellationToken ct = default)
    {
        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
    }
}
