using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Application.Contracts.Services;
using Statevia.Service.Api.Controllers;

namespace Statevia.Service.Api.Tests.Controllers;

/// <summary><see cref="SchedulesController"/> はサービスへ委譲する。</summary>
public sealed class SchedulesControllerTests
{
    /// <summary>作成は 201 と Location を返す。</summary>
    [Fact]
    public async Task Create_ReturnsCreatedAtGet()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var service = new FakeScheduleService
        {
            Created = new ExecutionScheduleDetailDto { ScheduleId = scheduleId, Name = "nightly-job" }
        };
        var controller = new SchedulesController(service);

        // Act
        var result = await controller.Create(new CreateExecutionScheduleRequest { Name = "nightly-job" }, CancellationToken.None);

        // Assert
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(SchedulesController.Get), created.ActionName);
        Assert.Equal(scheduleId, created.RouteValues?["scheduleId"]);
    }

    /// <summary>削除は 204。</summary>
    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        // Arrange
        var controller = new SchedulesController(new FakeScheduleService());

        // Act
        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    private sealed class FakeScheduleService : IExecutionScheduleService
    {
        public ExecutionScheduleDetailDto Created { get; init; } = new();

        public Task<IReadOnlyList<ExecutionScheduleListItemDto>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExecutionScheduleListItemDto>>([]);

        public Task<ExecutionScheduleDetailDto> GetAsync(Guid scheduleId, CancellationToken cancellationToken) =>
            Task.FromResult(new ExecutionScheduleDetailDto { ScheduleId = scheduleId });

        public Task<ExecutionScheduleDetailDto> CreateAsync(
            CreateExecutionScheduleRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Created);

        public Task<ExecutionScheduleDetailDto> UpdateAsync(
            Guid scheduleId,
            UpdateExecutionScheduleRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ExecutionScheduleDetailDto { ScheduleId = scheduleId });

        public Task DeleteAsync(Guid scheduleId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
