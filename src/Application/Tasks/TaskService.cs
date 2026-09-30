using Application.Abstractions;
using Application.Common;

namespace Application.Tasks;

public sealed class TaskService(ITaskRepository repository, TimeProvider timeProvider)
{
    public const int MaxTitleLength = 100;

    public Task<TaskResponse> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new AppValidationException("Title is required.");
        if (request.Title.Trim().Length > MaxTitleLength)
            throw new AppValidationException($"Title must be at most {MaxTitleLength} characters.");
        if (!Enum.IsDefined(request.Status))
            throw new AppValidationException($"'{(int)request.Status}' is not a valid status.");

        throw new NotImplementedException();
    }
}
