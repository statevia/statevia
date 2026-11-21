using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Service.Api.Scenario.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Statevia.Service.Api.Scenario.Tests.Scenarios;

/// <summary>
/// 実行コマンド（Start / Resume / Cancel）の HTTP E2E シナリオ（P0）。
/// </summary>
/// <remarks>
/// <para>PostgreSQL・API 内 Worker・Engine・投影を通し、コマンドの受理（201 / 204 / 422 / 409）と、
/// Worker 適用後の収束（GET execution / graph / waits / events）を分けて断言する。</para>
/// <para>収束は固定 Sleep ではなく <see cref="ExecutionSettleProbe"/> の poll で待つ。主アサートは GET で、
/// 未完了 work item ゼロだけ補助として DB を読む。</para>
/// <para>範囲外: Fork / Join、DelayWait、Ownership Recovery、定義 YAML の 422、Studio UI。</para>
/// </remarks>
[Trait("Category", "Scenario")]
[Collection(nameof(PostgresScenarioCollection))]
public sealed class ExecutionCommandScenarioTests : ScenarioTestBase
{
    private const string WaitNodeName = "WaitNode";
    private const string AllowedResumeEvent = "ResumeEvt";
    private const string CompletedStatus = "COMPLETED";
    private const string CanceledStatus = "CANCELED";
    private const string CompletedFact = "Completed";
    private const string StatusChangedEvent = "ExecutionStatusChanged";

    private readonly ScenarioDatabaseSeeder _seeder;
    private readonly ScenarioHttpClient _http;

    /// <summary>テストクラスを初期化する。</summary>
    /// <param name="fixture">PostgreSQL コンテナフィクスチャ。</param>
    /// <param name="output">xUnit テスト出力ヘルパー。</param>
    public ExecutionCommandScenarioTests(PostgresScenarioFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _seeder = new ScenarioDatabaseSeeder(fixture.ConnectionString);
        _http = new ScenarioHttpClient(fixture.Factory!);
    }

    /// <summary>直線の noop 定義を Start すると、受理は ACTIVE、収束後は COMPLETED で未完了 work item が残らない。</summary>
    [SkippableFact]
    public async Task Start_LinearNoopWorkflow_ConvergesToCompleted()
    {
        // Arrange
        SkipIfDockerUnavailable();
        using var arrangement = await ArrangeAsync(WorkflowYamlFixtures.LinearNoopWorkflow);

        // Act: 受理
        var started = await StartAsync(arrangement);

        // Assert: 受理時点は ACTIVE（完了とみなさない）
        Assert.Equal("ACTIVE", started.Status);

        // Assert: 収束
        var settled = await ExecutionSettleProbe.WaitUntilAsync(
            arrangement.Client,
            started.DisplayId,
            snapshot => snapshot.Status == CompletedStatus,
            "status=COMPLETED");
        Assert.Equal(2, settled.Nodes.Count);
        Assert.All(settled.Nodes, node => Assert.Equal(CompletedFact, node.Fact));
        Assert.Empty(settled.Waits);
        Assert.Contains(new ObservedEvent(StatusChangedEvent, "ACTIVE"), settled.Events);
        Assert.Equal(0, await CountOutstandingWorkItemsAsync(started.ResourceId));
    }

    /// <summary>Wait 1 ノードの定義を Start すると、収束後にノードが WAITING で waits が 1 件（nodeId と許可イベント）になる。</summary>
    [SkippableFact]
    public async Task Start_WaitWorkflow_ConvergesToSingleWaitingNode()
    {
        // Arrange
        SkipIfDockerUnavailable();
        using var arrangement = await ArrangeAsync(WorkflowYamlFixtures.WaitWorkflow);

        // Act
        var started = await StartAsync(arrangement);
        var settled = await ExecutionSettleProbe.WaitUntilAsync(
            arrangement.Client,
            started.DisplayId,
            snapshot => snapshot.Waits.Count == 1,
            "waits が 1 件");

        // Assert
        var wait = Assert.Single(settled.Waits);
        Assert.Equal(WaitNodeName, wait.NodeName);
        Assert.False(string.IsNullOrEmpty(wait.NodeId));
        Assert.Equal([AllowedResumeEvent], wait.AllowedEvents);
        Assert.Equal("ACTIVE", settled.Status);
        Assert.Contains(settled.Nodes, node => node.NodeId == wait.NodeId && node.NodeName == WaitNodeName);
    }

    /// <summary>許可イベントで Resume すると 204 のあと COMPLETED へ収束し、waits が空になる。</summary>
    [SkippableFact]
    public async Task Resume_WithAllowedEvent_ConvergesToCompletedWithoutWaits()
    {
        // Arrange
        SkipIfDockerUnavailable();
        using var arrangement = await ArrangeAsync(WorkflowYamlFixtures.WaitWorkflow);
        var started = await StartAsync(arrangement);
        var waiting = await ExecutionSettleProbe.WaitUntilAsync(
            arrangement.Client,
            started.DisplayId,
            snapshot => snapshot.Waits.Count == 1,
            "waits が 1 件");
        var nodeId = Assert.Single(waiting.Waits).NodeId;

        // Act
        var resume = await ResumeAsync(arrangement, started.DisplayId, nodeId, AllowedResumeEvent);

        // Assert: 受理は 204、収束後に完了
        Assert.Equal(HttpStatusCode.NoContent, resume.StatusCode);
        var settled = await ExecutionSettleProbe.WaitUntilAsync(
            arrangement.Client,
            started.DisplayId,
            snapshot => snapshot.Status == CompletedStatus && snapshot.Waits.Count == 0,
            "status=COMPLETED かつ waits が空");
        Assert.Empty(settled.Waits);
        Assert.Equal(0, await CountOutstandingWorkItemsAsync(started.ResourceId));
    }

    /// <summary>許可外イベントで Resume すると 422 になり、実行の status と waits は変わらない。</summary>
    [SkippableFact]
    public async Task Resume_WithDisallowedEvent_Returns422AndKeepsState()
    {
        // Arrange
        SkipIfDockerUnavailable();
        using var arrangement = await ArrangeAsync(WorkflowYamlFixtures.WaitWorkflow);
        var started = await StartAsync(arrangement);
        var before = await ExecutionSettleProbe.WaitUntilAsync(
            arrangement.Client,
            started.DisplayId,
            snapshot => snapshot.Waits.Count == 1,
            "waits が 1 件");
        var waitBefore = Assert.Single(before.Waits);

        // Act
        var resume = await ResumeAsync(arrangement, started.DisplayId, waitBefore.NodeId, "NotAllowedEvt");

        // Assert: 受理されず、状態は不変
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resume.StatusCode);
        var after = await ExecutionSettleProbe.ObserveAsync(arrangement.Client, started.DisplayId);
        Assert.Equal(before.Status, after.Status);
        var waitAfter = Assert.Single(after.Waits);
        Assert.Equal(waitBefore.NodeId, waitAfter.NodeId);
        Assert.Equal(waitBefore.AllowedEvents, waitAfter.AllowedEvents);
    }

    /// <summary>Start 受理直後に Cancel すると 204 で受理され、収束後は CANCELED で未完了 work item が残らない。</summary>
    /// <remarks>Worker が Start と Cancel のどちらを先に適用しても終端は CANCELED になる。内部の適用順は断言しない。</remarks>
    [SkippableFact]
    public async Task Cancel_RightAfterStart_ConvergesToCanceledWithoutOutstandingWorkItems()
    {
        // Arrange
        SkipIfDockerUnavailable();
        using var arrangement = await ArrangeAsync(WorkflowYamlFixtures.WaitWorkflow);
        var started = await StartAsync(arrangement);

        // Act
        var cancel = await CancelAsync(arrangement, started.DisplayId);

        // Assert: 受理は 204。終端への収束は Worker の適用後
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var accepted = await ExecutionSettleProbe.ObserveAsync(arrangement.Client, started.DisplayId);
        Assert.True(accepted.CancelRequested);
        var settled = await ExecutionSettleProbe.WaitUntilAsync(
            arrangement.Client,
            started.DisplayId,
            snapshot => snapshot.Status == CanceledStatus,
            "status=CANCELED");
        Assert.Empty(settled.Waits);
        Assert.Contains(new ObservedEvent(StatusChangedEvent, CanceledStatus), settled.Events);
        Assert.Equal(0, await CountOutstandingWorkItemsAsync(started.ResourceId));
    }

    /// <summary>Wait に到達してから Cancel すると、収束後は CANCELED、waits は空、キャンセルの ExecutionStatusChanged（to=CANCELED）が観測できる。</summary>
    [SkippableFact]
    public async Task Cancel_WhileWaiting_ConvergesToCanceledWithEmptyWaits()
    {
        // Arrange
        SkipIfDockerUnavailable();
        using var arrangement = await ArrangeAsync(WorkflowYamlFixtures.WaitWorkflow);
        var started = await StartAsync(arrangement);
        await ExecutionSettleProbe.WaitUntilAsync(
            arrangement.Client,
            started.DisplayId,
            snapshot => snapshot.Waits.Count == 1,
            "waits が 1 件");

        // Act
        var cancel = await CancelAsync(arrangement, started.DisplayId);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var settled = await ExecutionSettleProbe.WaitUntilAsync(
            arrangement.Client,
            started.DisplayId,
            snapshot => snapshot.Status == CanceledStatus && snapshot.Waits.Count == 0,
            "status=CANCELED かつ waits が空");
        Assert.Contains(new ObservedEvent(StatusChangedEvent, CanceledStatus), settled.Events);
        Assert.Equal(0, await CountOutstandingWorkItemsAsync(started.ResourceId));
    }

    /// <summary>同じ冪等キーで Cancel を再送しても 204 で、終端は 1 回だけ（キャンセルの ExecutionStatusChanged は 1 件）で未完了 work item も増えない。</summary>
    [SkippableFact]
    public async Task Cancel_ResentWithSameIdempotencyKey_AppliesOnce()
    {
        // Arrange
        SkipIfDockerUnavailable();
        using var arrangement = await ArrangeAsync(WorkflowYamlFixtures.WaitWorkflow);
        var started = await StartAsync(arrangement);
        var idempotencyKey = Guid.NewGuid().ToString("N");

        // Act
        var first = await CancelAsync(arrangement, started.DisplayId, idempotencyKey);
        var second = await CancelAsync(arrangement, started.DisplayId, idempotencyKey);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var settled = await ExecutionSettleProbe.WaitUntilAsync(
            arrangement.Client,
            started.DisplayId,
            snapshot => snapshot.Status == CanceledStatus,
            "status=CANCELED");
        Assert.Single(settled.Events, observed => observed == new ObservedEvent(StatusChangedEvent, CanceledStatus));
        Assert.Equal(0, await CountOutstandingWorkItemsAsync(started.ResourceId));
    }

    /// <summary>同じ冪等キーでも本文（input）が違う Start は 409 <c>IDEMPOTENCY_KEY_CONFLICT</c> になり、最初の実行は影響を受けない。</summary>
    [SkippableFact]
    public async Task Start_WithSameIdempotencyKeyAndDifferentBody_Returns409()
    {
        // Arrange
        SkipIfDockerUnavailable();
        using var arrangement = await ArrangeAsync(WorkflowYamlFixtures.WaitWorkflow);
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var first = await StartAsync(arrangement, idempotencyKey, new { value = 1 });

        // Act
        using var conflict = await SendStartAsync(arrangement, idempotencyKey, new { value = 2 });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await ScenarioHttpClient.AssertErrorCodeAsync(conflict, "IDEMPOTENCY_KEY_CONFLICT");
        var settled = await ExecutionSettleProbe.WaitUntilAsync(
            arrangement.Client,
            first.DisplayId,
            snapshot => snapshot.Waits.Count == 1,
            "waits が 1 件");
        Assert.Equal("ACTIVE", settled.Status);
    }

    private async Task<Arrangement> ArrangeAsync(Func<string, string> yamlFactory)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"user-{suffix}";
        var principalId = await _seeder.SeedUserAsync(username, "Password1!");
        var projectId = await _seeder.SeedProjectWithAccessAsync(
            ScenarioTenantIds.DefaultTenantId,
            principalId,
            ProjectAccessRole.Admin);
        var login = await _http.LoginAsync("default", username, "Password1!");
        var client = _http.CreateAuthenticatedClient(login.AccessToken, "default");

        var yaml = yamlFactory(suffix);
        var createResponse = await client.PostAsJsonAsync("/v1/definitions", new
        {
            name = $"scenario-command-{suffix}",
            yaml,
            projectId
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var definition = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        return new Arrangement(client, definition.GetProperty("displayId").GetString()!);
    }

    private static async Task<StartedExecution> StartAsync(
        Arrangement arrangement,
        string? idempotencyKey = null,
        object? input = null)
    {
        using var response = await SendStartAsync(arrangement, idempotencyKey, input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new StartedExecution(
            body.GetProperty("displayId").GetString()!,
            body.GetProperty("resourceId").GetGuid(),
            body.GetProperty("status").GetString()!);
    }

    private static async Task<HttpResponseMessage> SendStartAsync(
        Arrangement arrangement,
        string? idempotencyKey,
        object? input)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/executions")
        {
            Content = JsonContent.Create(new { definitionId = arrangement.DefinitionId, input })
        };
        if (idempotencyKey is not null)
            request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        return await arrangement.Client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> CancelAsync(
        Arrangement arrangement,
        string executionId,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/executions/{executionId}/cancel")
        {
            Content = JsonContent.Create(new { })
        };
        if (idempotencyKey is not null)
            request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        return await arrangement.Client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> ResumeAsync(
        Arrangement arrangement,
        string executionId,
        string nodeId,
        string resumeKey) =>
        arrangement.Client.PostAsJsonAsync(
            $"/v1/executions/{executionId}/nodes/{Uri.EscapeDataString(nodeId)}/resume",
            new { resumeKey });

    /// <summary>補助アサート: 当該実行に未完了（キューに残っている）work item の件数。完了した item は削除される。</summary>
    private async Task<int> CountOutstandingWorkItemsAsync(Guid executionResourceId)
    {
        using var scope = Fixture.Factory!.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>();
        await using var context = await dbFactory.CreateDbContextAsync();
        return await context.ExecutionWorkItems.CountAsync(item => item.ExecutionId == executionResourceId);
    }

    private sealed record StartedExecution(string DisplayId, Guid ResourceId, string Status);

    private sealed class Arrangement(HttpClient client, string definitionId) : IDisposable
    {
        public HttpClient Client { get; } = client;

        public string DefinitionId { get; } = definitionId;

        public void Dispose() => Client.Dispose();
    }
}
