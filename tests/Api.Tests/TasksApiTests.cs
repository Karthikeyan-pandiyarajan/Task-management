using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Tasks;
using Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests;

[Collection(ApiCollection.Name)]
public class TasksApiTests(ApiFixture fixture)
{
    // The API serializes enums as strings ("Todo"), so the test client must read them the same way.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    [Fact]
    public async Task Requests_without_token_return_401()
    {
        using var anonymous = fixture.TasksApi.CreateClient();

        var response = await anonymous.GetAsync("/api/tasks");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Requests_with_tampered_token_return_401()
    {
        using var client = fixture.TasksApi.CreateClient();
        var token = await fixture.RegisterNewUserAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        var response = await client.GetAsync("/api/tasks");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_returns_201_with_location_of_new_task()
    {
        using var client = await fixture.CreateAuthenticatedTasksClientAsync();

        var response = await client.PostAsJsonAsync("/api/tasks", NewTask("Write tests"), Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<TaskResponse>(Json);
        created!.Title.Should().Be("Write tests");
        response.Headers.Location!.ToString().Should().EndWith($"/api/tasks/{created.Id}");

        var fetched = await client.GetFromJsonAsync<TaskResponse>(response.Headers.Location, Json);
        fetched.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task Create_ignores_any_user_id_in_the_body()
    {
        using var client = await fixture.CreateAuthenticatedTasksClientAsync();
        var payload = new { title = "Sneaky", status = "Todo", dueDate = Tomorrow, userId = Guid.NewGuid() };

        var response = await client.PostAsJsonAsync("/api/tasks", payload, Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.GetFromJsonAsync<List<TaskResponse>>("/api/tasks", Json)).Should().ContainSingle(t => t.Title == "Sneaky");
    }

    [Fact]
    public async Task Create_with_invalid_data_returns_400_problem_details()
    {
        using var client = await fixture.CreateAuthenticatedTasksClientAsync();

        var response = await client.PostAsJsonAsync("/api/tasks", NewTask("   "), Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status.Should().Be(400);
    }

    [Fact]
    public async Task Create_with_past_due_date_returns_400()
    {
        using var client = await fixture.CreateAuthenticatedTasksClientAsync();

        var response = await client.PostAsJsonAsync("/api/tasks", NewTask("Late") with { DueDate = Tomorrow.AddDays(-5) }, Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_missing_task_returns_404()
    {
        using var client = await fixture.CreateAuthenticatedTasksClientAsync();

        var response = await client.GetAsync($"/api/tasks/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_returns_only_own_tasks_and_applies_status_filter()
    {
        using var client = await fixture.CreateAuthenticatedTasksClientAsync();
        await CreateAsync(client, NewTask("Todo one"));
        await CreateAsync(client, NewTask("Done one") with { Status = TaskItemStatus.Done });

        var all = await client.GetFromJsonAsync<List<TaskResponse>>("/api/tasks", Json);
        var done = await client.GetFromJsonAsync<List<TaskResponse>>("/api/tasks?status=Done", Json);

        all!.Select(t => t.Title).Should().BeEquivalentTo("Todo one", "Done one");
        done!.Should().ContainSingle().Which.Title.Should().Be("Done one");
    }

    [Fact]
    public async Task List_with_unknown_status_returns_400()
    {
        using var client = await fixture.CreateAuthenticatedTasksClientAsync();

        var response = await client.GetAsync("/api/tasks?status=99");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_returns_200_with_changes()
    {
        using var client = await fixture.CreateAuthenticatedTasksClientAsync();
        var created = await CreateAsync(client, NewTask("Original"));
        var update = new UpdateTaskRequest("Renamed", "notes", TaskItemStatus.InProgress, Tomorrow.AddDays(3));

        var response = await client.PutAsJsonAsync($"/api/tasks/{created.Id}", update, Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TaskResponse>(Json);
        updated.Should().BeEquivalentTo(new TaskResponse(created.Id, "Renamed", "notes", TaskItemStatus.InProgress, update.DueDate));
    }

    [Fact]
    public async Task Delete_returns_204_and_task_is_gone()
    {
        using var client = await fixture.CreateAuthenticatedTasksClientAsync();
        var created = await CreateAsync(client, NewTask("Temporary"));

        var response = await client.DeleteAsync($"/api/tasks/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/tasks/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_B_cannot_see_change_or_delete_user_As_task()
    {
        using var userA = await fixture.CreateAuthenticatedTasksClientAsync();
        using var userB = await fixture.CreateAuthenticatedTasksClientAsync();
        var aTask = await CreateAsync(userA, NewTask("A's private task"));
        var url = $"/api/tasks/{aTask.Id}";

        (await userB.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await userB.PutAsJsonAsync(url, new UpdateTaskRequest("Hijacked", null, TaskItemStatus.Done, Tomorrow), Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await userB.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await userB.GetFromJsonAsync<List<TaskResponse>>("/api/tasks", Json)).Should().BeEmpty();

        // A's task is untouched.
        var stillThere = await userA.GetFromJsonAsync<TaskResponse>(url, Json);
        stillThere!.Title.Should().Be("A's private task");
    }

    private static CreateTaskRequest NewTask(string title)
        => new(title, "details", TaskItemStatus.Todo, Tomorrow);

    private static async Task<TaskResponse> CreateAsync(HttpClient client, CreateTaskRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskResponse>(Json))!;
    }
}
