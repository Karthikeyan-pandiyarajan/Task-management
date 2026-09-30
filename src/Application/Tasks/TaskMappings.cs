using Domain.Entities;

namespace Application.Tasks;

internal static class TaskMappings
{
    public static TaskResponse ToResponse(this TaskItem task)
        => new(task.Id, task.Title, task.Description, task.Status, task.DueDate);
}
