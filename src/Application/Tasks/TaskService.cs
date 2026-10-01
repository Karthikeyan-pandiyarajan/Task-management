using Application.Abstractions;
using Application.Common;
using Domain.Entities;

namespace Application.Tasks;

public sealed class TaskService(ITaskRepository repository, TimeProvider timeProvider)
{
    public const int MaxTitleLength = 100;

    public async Task<TaskResponse> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct = default)
    {
        ValidateFields(request.Title, request.Status);
        // Date-only comparison: a task due "today" is still valid regardless of the time of day.
        if (request.DueDate.Date < timeProvider.GetUtcNow().UtcDateTime.Date)
            throw new AppValidationException("Due date cannot be in the past.");

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description,
            Status = request.Status,
            DueDate = request.DueDate,
            UserId = userId
        };

        await repository.AddAsync(task, ct);

        return task.ToResponse();
    }

    public async Task<TaskResponse> GetByIdAsync(Guid userId, Guid taskId, CancellationToken ct = default)
        => (await GetOwnedTaskAsync(userId, taskId, ct)).ToResponse();

    public async Task<IReadOnlyList<TaskResponse>> ListAsync(Guid userId, TaskItemStatus? status, CancellationToken ct = default)
    {
        if (status is not null)
            ValidateStatus(status.Value);

        var tasks = await repository.GetByUserAsync(userId, status, ct);
        return tasks.Select(t => t.ToResponse()).ToList();
    }

    // The past-date rule applies to create only, so an overdue task can still be edited or marked Done.
    public async Task<TaskResponse> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var task = await GetOwnedTaskAsync(userId, taskId, ct);
        ValidateFields(request.Title, request.Status);

        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.Status = request.Status;
        task.DueDate = request.DueDate;

        await repository.UpdateAsync(task, ct);

        return task.ToResponse();
    }

    public async Task DeleteAsync(Guid userId, Guid taskId, CancellationToken ct = default)
    {
        var task = await GetOwnedTaskAsync(userId, taskId, ct);
        await repository.DeleteAsync(task, ct);
    }

    private static void ValidateFields(string? title, TaskItemStatus status)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new AppValidationException("Title is required.");
        if (title.Trim().Length > MaxTitleLength)
            throw new AppValidationException($"Title must be at most {MaxTitleLength} characters.");
        ValidateStatus(status);
    }

    private static void ValidateStatus(TaskItemStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new AppValidationException($"'{(int)status}' is not a valid status.");
    }

    // Missing and not-owned produce the same exception so callers can't probe which task IDs exist.
    private async Task<TaskItem> GetOwnedTaskAsync(Guid userId, Guid taskId, CancellationToken ct)
    {
        var task = await repository.GetByIdAsync(taskId, ct);
        if (task is null || task.UserId != userId)
            throw new NotFoundException($"Task {taskId} was not found.");

        return task;
    }
}
