using Application.Abstractions;

namespace Application.Tasks;

public sealed class TaskService(ITaskRepository repository, TimeProvider timeProvider)
{
    public Task<TaskResponse> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct = default)
        => throw new NotImplementedException();
}
