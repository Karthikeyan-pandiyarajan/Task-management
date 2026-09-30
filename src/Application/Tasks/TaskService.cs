using Application.Abstractions;
using Application.Common;
using Domain.Entities;

namespace Application.Tasks;

public sealed class TaskService(ITaskRepository repository, TimeProvider timeProvider)
{
    public const int MaxTitleLength = 100;

    public async Task<TaskResponse> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new AppValidationException("Title is required.");
        if (request.Title.Trim().Length > MaxTitleLength)
            throw new AppValidationException($"Title must be at most {MaxTitleLength} characters.");
        if (!Enum.IsDefined(request.Status))
            throw new AppValidationException($"'{(int)request.Status}' is not a valid status.");
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
    {
        var task = await repository.GetByIdAsync(taskId, ct);
        // Same exception as "missing" so callers can't probe which task IDs exist.
        if (task!.UserId != userId)
            throw new NotFoundException($"Task {taskId} was not found.");

        return task.ToResponse();
    }
}
