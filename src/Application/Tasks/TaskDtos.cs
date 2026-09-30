using Domain.Entities;

namespace Application.Tasks;

public sealed record CreateTaskRequest(string Title, string? Description, TaskItemStatus Status, DateTime DueDate);

public sealed record UpdateTaskRequest(string Title, string? Description, TaskItemStatus Status, DateTime DueDate);

public sealed record TaskResponse(Guid Id, string Title, string? Description, TaskItemStatus Status, DateTime DueDate);
