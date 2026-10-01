using System.Security.Claims;
using Api.Tasks.Controllers;
using Application.Abstractions;
using Application.Common;
using Application.Tasks;
using Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Api.Tests.Controllers;

// Isolated controller tests: no HTTP pipeline, no database. They prove the controller only translates
// between HTTP and TaskService, and that the user id always comes from the token's "sub" claim.
public class TasksControllerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private readonly Mock<ITaskRepository> _repository = new();
    private readonly TasksController _sut;

    public TasksControllerTests()
    {
        _sut = new TasksController(new TaskService(_repository.Object, TimeProvider.System))
        {
            ControllerContext = ContextFor(new Claim("sub", UserId.ToString()))
        };
    }

    [Fact]
    public async Task Create_returns_201_pointing_at_Get_with_the_new_id()
    {
        var result = await _sut.Create(new CreateTaskRequest("Write tests", null, TaskItemStatus.Todo, Tomorrow), default);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var body = created.Value.Should().BeOfType<TaskResponse>().Subject;
        created.ActionName.Should().Be(nameof(TasksController.Get));
        created.RouteValues!["id"].Should().Be(body.Id);
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task Create_saves_the_task_for_the_user_in_the_token()
    {
        await _sut.Create(new CreateTaskRequest("Mine", null, TaskItemStatus.Todo, Tomorrow), default);

        _repository.Verify(r => r.AddAsync(It.Is<TaskItem>(t => t.UserId == UserId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_queries_only_the_token_users_tasks_with_the_status_filter()
    {
        _repository
            .Setup(r => r.GetByUserAsync(UserId, TaskItemStatus.Done, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TaskItem { Id = Guid.NewGuid(), Title = "Done one", Status = TaskItemStatus.Done, UserId = UserId }]);

        var result = await _sut.List(TaskItemStatus.Done, default);

        result.Should().ContainSingle().Which.Title.Should().Be("Done one");
        _repository.Verify(r => r.GetByUserAsync(UserId, TaskItemStatus.Done, It.IsAny<CancellationToken>()), Times.Once);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Get_returns_the_task_dto()
    {
        var task = GivenTaskOwnedBy(UserId);

        var result = await _sut.Get(task.Id, default);

        result.Should().BeEquivalentTo(new TaskResponse(task.Id, task.Title, task.Description, task.Status, task.DueDate));
    }

    [Fact]
    public async Task Get_lets_NotFoundException_bubble_up_for_another_users_task()
    {
        var othersTask = GivenTaskOwnedBy(Guid.NewGuid());

        var act = () => _sut.Get(othersTask.Id, default);

        // No try/catch in the controller: GlobalExceptionHandler turns this into a 404.
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_returns_the_updated_dto()
    {
        var task = GivenTaskOwnedBy(UserId);

        var result = await _sut.Update(task.Id, new UpdateTaskRequest("Renamed", "n", TaskItemStatus.Done, Tomorrow), default);

        result.Title.Should().Be("Renamed");
        result.Status.Should().Be(TaskItemStatus.Done);
    }

    [Fact]
    public async Task Delete_returns_204_and_removes_the_task()
    {
        var task = GivenTaskOwnedBy(UserId);

        var result = await _sut.Delete(task.Id, default);

        result.Should().BeOfType<NoContentResult>();
        _repository.Verify(r => r.DeleteAsync(task, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Token_without_a_valid_user_id_is_rejected_before_any_data_access()
    {
        _sut.ControllerContext = ContextFor(new Claim("sub", "not-a-guid"));

        var act = () => _sut.List(null, default);

        await act.Should().ThrowAsync<UnauthorizedException>();
        _repository.VerifyNoOtherCalls();
    }

    private TaskItem GivenTaskOwnedBy(Guid ownerId)
    {
        var task = new TaskItem { Id = Guid.NewGuid(), Title = "Existing", Status = TaskItemStatus.Todo, DueDate = Tomorrow, UserId = ownerId };
        _repository.Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        return task;
    }

    private static ControllerContext ContextFor(params Claim[] claims) => new()
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
    };
}
