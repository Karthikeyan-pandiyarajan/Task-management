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

    [Fact]
    public async Task Update_task_owned_by_another_user_throws_not_found_and_never_writes()
    {
        var othersTask = ExistingTask(ownerId: Guid.NewGuid());

        var act = () => _sut.UpdateAsync(UserId, othersTask.Id, ValidUpdate());

        await act.Should().ThrowAsync<NotFoundException>();
        othersTask.Title.Should().Be("Existing");
        _repository.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_task_owned_by_another_user_throws_not_found_and_never_deletes()
    {
        var othersTask = ExistingTask(ownerId: Guid.NewGuid());

        var act = () => _sut.DeleteAsync(UserId, othersTask.Id);

        await act.Should().ThrowAsync<NotFoundException>();
        _repository.Verify(r => r.DeleteAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TaskItemStatus.Done)]
    public async Task List_passes_status_filter_to_repository(TaskItemStatus? status)
    {
        var owned = new TaskItem { Id = Guid.NewGuid(), Title = "Mine", Status = TaskItemStatus.Done, UserId = UserId };
        _repository
            .Setup(r => r.GetByUserAsync(UserId, status, It.IsAny<CancellationToken>()))
            .ReturnsAsync([owned]);

        var result = await _sut.ListAsync(UserId, status);

        result.Should().ContainSingle().Which.Id.Should().Be(owned.Id);
        _repository.Verify(r => r.GetByUserAsync(UserId, status, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_with_undefined_status_filter_throws()
    {
        var act = () => _sut.ListAsync(UserId, (TaskItemStatus)99);

        await act.Should().ThrowAsync<AppValidationException>();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_own_task_changes_fields_and_saves()
    {
        var mine = ExistingTask(ownerId: UserId);
        var request = new UpdateTaskRequest("Renamed", "New notes", TaskItemStatus.Done, Now.UtcDateTime.AddDays(5));

        var result = await _sut.UpdateAsync(UserId, mine.Id, request);

        result.Should().BeEquivalentTo(new TaskResponse(mine.Id, "Renamed", "New notes", TaskItemStatus.Done, request.DueDate));
        mine.UserId.Should().Be(UserId);
        _repository.Verify(r => r.UpdateAsync(mine, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_allows_past_due_date_so_overdue_tasks_can_be_completed()
    {
        var mine = ExistingTask(ownerId: UserId);
        var overdue = ValidUpdate() with { Status = TaskItemStatus.Done, DueDate = Now.UtcDateTime.AddDays(-10) };

        var result = await _sut.UpdateAsync(UserId, mine.Id, overdue);

        result.Status.Should().Be(TaskItemStatus.Done);
    }

    [Theory]
    [InlineData("", TaskItemStatus.Todo)]
    [InlineData("ok", (TaskItemStatus)42)]
    public async Task Update_with_invalid_data_throws_and_never_writes(string title, TaskItemStatus status)
    {
        var mine = ExistingTask(ownerId: UserId);

        var act = () => _sut.UpdateAsync(UserId, mine.Id, ValidUpdate() with { Title = title, Status = status });

        await act.Should().ThrowAsync<AppValidationException>();
        _repository.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_with_title_over_100_characters_throws()
    {
        var mine = ExistingTask(ownerId: UserId);

        var act = () => _sut.UpdateAsync(UserId, mine.Id, ValidUpdate() with { Title = new string('x', 101) });

        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task Delete_own_task_removes_it()
    {
        var mine = ExistingTask(ownerId: UserId);

        await _sut.DeleteAsync(UserId, mine.Id);

        _repository.Verify(r => r.DeleteAsync(mine, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static UpdateTaskRequest ValidUpdate()
        => new("Updated title", null, TaskItemStatus.InProgress, Now.UtcDateTime.AddDays(2));

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
