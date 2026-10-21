using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Controllers;

namespace Statevia.Service.Api.Tests.Controllers;

/// <summary><see cref="ProjectsController"/> の HTTP 写像。</summary>
public sealed class ProjectsControllerTests
{
    /// <summary>作成結果を CreatedAtAction で返す。</summary>
    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var fake = new FakeProjectService
        {
            CreateResult = new ProjectResponse
            {
                ProjectId = projectId,
                Slug = "p1",
                DisplayName = "P1",
                Role = "admin",
                CreatedAt = DateTime.UtcNow
            }
        };
        var controller = new ProjectsController(fake)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var res = await controller.Create(
            new CreateProjectRequest { Slug = "p1", DisplayName = "P1" },
            CancellationToken.None);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(res.Result);
        Assert.Equal(nameof(controller.Get), created.ActionName);
        Assert.Equal(projectId, created.RouteValues!["projectId"]);
    }

    /// <summary>Project 本体の DELETE アクションは無い。</summary>
    [Fact]
    public void Controller_HasNoDeleteAction()
    {
        // Arrange & Act
        var deleteMethods = typeof(ProjectsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<HttpDeleteAttribute>() is { } delete
                && (delete.Template is null
                    || !delete.Template.Contains("members", StringComparison.Ordinal)))
            .ToList();

        // Assert
        Assert.Empty(deleteMethods);
    }

    /// <summary>PATCH が更新結果を返す。</summary>
    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var fake = new FakeProjectService
        {
            UpdateResult = new ProjectResponse
            {
                ProjectId = projectId,
                Slug = "renamed",
                DisplayName = "P1",
                Role = "admin",
                CreatedAt = DateTime.UtcNow
            }
        };
        var controller = new ProjectsController(fake);

        // Act
        var res = await controller.Update(
            projectId,
            new UpdateProjectRequest { Slug = "renamed" },
            CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var body = Assert.IsType<ProjectResponse>(ok.Value);
        Assert.Equal("renamed", body.Slug);
    }

    private sealed class FakeProjectService : IProjectService
    {
        public ProjectResponse CreateResult { get; set; } = new();
        public ProjectResponse UpdateResult { get; set; } = new();
        public ProjectResponse GetResult { get; set; } = new();
        public IReadOnlyList<ProjectResponse> ListResult { get; set; } = [];

        public Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken ct) =>
            Task.FromResult(ListResult);

        public Task<ProjectResponse> GetAsync(Guid projectId, CancellationToken ct) =>
            Task.FromResult(GetResult);

        public Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken ct) =>
            Task.FromResult(CreateResult);

        public Task<ProjectResponse> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct) =>
            Task.FromResult(UpdateResult);

        public Task<IReadOnlyList<ProjectMemberResponse>> ListMembersAsync(Guid projectId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProjectMemberResponse>>([]);

        public Task UpsertMemberAsync(
            Guid projectId,
            Guid principalId,
            UpsertProjectMemberRequest request,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task RemoveMemberAsync(Guid projectId, Guid principalId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ProjectResponse>> ListAllForTenantAdminAsync(CancellationToken ct) =>
            Task.FromResult(ListResult);
    }
}
