using Application.Abstractions;
using Application.Common;
using Application.Tasks;
using Domain.Entities;
using FluentAssertions;
using Moq;

namespace Application.Tests.Tasks;

public class TaskServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<ITaskRepository> _repository = new();
    private readonly TaskService _sut;

    public TaskServiceTests()
    {
        _sut = new TaskService(_repository.Object, new FixedTimeProvider(Now));
    }

    private static CreateTaskRequest ValidCreate(
        string title = "Write report",
        TaskItemStatus status = TaskItemStatus.Todo,
        DateTime? dueDate = null)
        => new(title, "Quarterly numbers", status, dueDate ?? Now.UtcDateTime.AddDays(3));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_with_empty_title_throws_and_never_touches_repository(string? title)
    {
        var act = () => _sut.CreateAsync(UserId, ValidCreate(title: title!));

        await act.Should().ThrowAsync<AppValidationException>();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_with_title_over_100_characters_throws()
    {
        var act = () => _sut.CreateAsync(UserId, ValidCreate(title: new string('a', 101)));

        await act.Should().ThrowAsync<AppValidationException>().WithMessage("*100*");
        _repository.VerifyNoOtherCalls();
    }
}
