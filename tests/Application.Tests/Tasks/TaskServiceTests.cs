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

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public async Task Create_with_undefined_status_throws(int status)
    {
        var act = () => _sut.CreateAsync(UserId, ValidCreate(status: (TaskItemStatus)status));

        await act.Should().ThrowAsync<AppValidationException>().WithMessage("*status*");
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_with_due_date_in_the_past_throws()
    {
        var yesterday = Now.UtcDateTime.AddDays(-1);

        var act = () => _sut.CreateAsync(UserId, ValidCreate(dueDate: yesterday));

        await act.Should().ThrowAsync<AppValidationException>().WithMessage("*past*");
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_valid_task_adds_it_once_for_the_user_and_returns_it()
    {
        TaskItem? saved = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, CancellationToken>((t, _) => saved = t)
            .Returns(Task.CompletedTask);
        var request = ValidCreate();

        var result = await _sut.CreateAsync(UserId, request);

        _repository.Verify(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Once);
        saved!.UserId.Should().Be(UserId);
        saved.Id.Should().NotBeEmpty();
        result.Should().BeEquivalentTo(new TaskResponse(
            saved.Id, request.Title, request.Description, request.Status, request.DueDate));
    }

    [Fact]
    public async Task Get_task_owned_by_another_user_throws_not_found()
    {
        var othersTask = ExistingTask(ownerId: Guid.NewGuid());

        var act = () => _sut.GetByIdAsync(UserId, othersTask.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Get_missing_task_throws_not_found()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        var act = () => _sut.GetByIdAsync(UserId, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private TaskItem ExistingTask(Guid ownerId)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Existing",
            Status = TaskItemStatus.InProgress,
            DueDate = Now.UtcDateTime.AddDays(1),
            UserId = ownerId
        };
        _repository
            .Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        return task;
    }
}
