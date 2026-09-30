using Application.Abstractions;
using Application.Common;

namespace Application.Tasks;

public sealed class TaskService(ITaskRepository repository, TimeProvider timeProvider)
{
    public Task<TaskResponse> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new AppValidationException("Title is required.");

        throw new NotImplementedException();
    }
}
