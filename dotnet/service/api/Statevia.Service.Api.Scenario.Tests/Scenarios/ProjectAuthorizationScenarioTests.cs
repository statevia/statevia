using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Statevia.Service.Api.Scenario.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Statevia.Service.Api.Scenario.Tests.Scenarios;

/// <summary>
/// プロジェクト認可シナリオテスト。
/// </summary>
/// <remarks>
/// <para>同一テナントの Principal 差と、他テナントからの存在秘匿を HTTP で固定する。</para>
/// <list type="bullet">
///   <item>同一テナント未所属の定義 GET は 404</item>
///   <item>他テナントの定義 GET は 404</item>
///   <item>Reader の Start は 403 <c>PROJECT_ACCESS_DENIED</c></item>
///   <item>Executor の Start は 201</item>
///   <item><c>tenant.admin</c> 未所属の定義 GET は 404。自己昇格後は 200</item>
///   <item>未所属の Cancel は 404、Reader の Cancel は 403 <c>PROJECT_ACCESS_DENIED</c></item>
///   <item>未所属のイベント送信は、子の待機が 2 件で届け先が決まらない実行でも 404</item>
///   <item>Executor でも実行リソース許可の外なら、イベント送信は 403 <c>RESOURCE_GRANT_DENIED</c> で待機は残る</item>
///   <item>集合配送は呼び出し元が所属する Project の待機だけを再開し、所属しない Project の待機は待機のまま（どちらも 204）</item>
///   <item>集合配送の候補に、所属しない Project の実行の topic / key は出ない</item>
/// </list>
/// </remarks>
[Trait("Category", "Scenario")]
[Collection(nameof(PostgresScenarioCollection))]
public sealed class ProjectAuthorizationScenarioTests : ScenarioTestBase
{
    private readonly ScenarioDatabaseSeeder _seeder;
    private readonly ScenarioHttpClient _http;

    /// <summary>テストクラスを初期化する。</summary>
    public ProjectAuthorizationScenarioTests(PostgresScenarioFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _seeder = new ScenarioDatabaseSeeder(fixture.ConnectionString);
        _http = new ScenarioHttpClient(fixture.Factory!);
    }

    /// <summary>同一テナントで未所属のユーザーが定義 GET すると 404 になる。</summary>
    [SkippableFact]
    public async Task GetDefinition_SameTenantNonMember_Returns404()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (_, definitionId) = await CreateOwnedDefinitionAsync(suffix);
        var username = $"nomember-{suffix}";
        await _seeder.SeedUserInTenantAsync(
            ScenarioTenantIds.DefaultTenantId,
            username,
            "Password1!",
            isTenantAdmin: false,
            permissionKeys: [WellKnownPermissionKeys.DefinitionsRead]);
        var login = await _http.LoginAsync("default", username, "Password1!");
        using var client = _http.CreateAuthenticatedClient(login.AccessToken, "default");

        // Act
        var response = await client.GetAsync(
            new Uri($"/v1/definitions/{Uri.EscapeDataString(definitionId)}", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>他テナントのユーザーが定義 GET すると 404 になる。越境 grant は作らない。</summary>
    [SkippableFact]
    public async Task GetDefinition_OtherTenant_Returns404()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (_, definitionId) = await CreateOwnedDefinitionAsync(suffix);
        var consumerTenantId = await _seeder.SeedTenantAsync(suffix);
        var username = $"other-{suffix}";
        await _seeder.SeedUserInTenantAsync(
            consumerTenantId,
            username,
            "Password1!",
            isTenantAdmin: false,
            permissionKeys: [WellKnownPermissionKeys.DefinitionsRead]);
        var login = await _http.LoginAsync($"scenario-{suffix}", username, "Password1!");
        using var client = _http.CreateAuthenticatedClient(login.AccessToken, $"scenario-{suffix}");

        // Act
        var response = await client.GetAsync(
            new Uri($"/v1/definitions/{Uri.EscapeDataString(definitionId)}", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>同一テナントの Reader が Start すると 403 <c>PROJECT_ACCESS_DENIED</c> になる。</summary>
    [SkippableFact]
    public async Task StartExecution_WithReaderAccess_Returns403ProjectAccessDenied()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (projectId, definitionId) = await CreateOwnedDefinitionAsync(suffix);
        using var client = await CreateMemberClientAsync(suffix, projectId, ProjectAccessRole.Reader);

        // Act
        var response = await client.PostAsJsonAsync("/v1/executions", new { definitionId });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ScenarioHttpClient.AssertErrorCodeAsync(response, "PROJECT_ACCESS_DENIED");
    }

    /// <summary>同一テナントの Executor が Start すると 201 になる。</summary>
    [SkippableFact]
    public async Task StartExecution_WithExecutorAccess_Returns201Running()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (projectId, definitionId) = await CreateOwnedDefinitionAsync(suffix);
        using var client = await CreateMemberClientAsync(suffix, projectId, ProjectAccessRole.Executor);

        // Act
        var response = await client.PostAsJsonAsync("/v1/executions", new { definitionId });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var exec = await response.Content.ReadFromJsonAsync<JsonElement>();
        var status = exec.GetProperty("status").GetString();
        Assert.True(
            status is "ACTIVE",
            $"Expected ACTIVE, got: {status}");
    }

    /// <summary>未所属の tenant.admin は定義 GET が 404 のまま、自己昇格後は 200 になる。</summary>
    [SkippableFact]
    public async Task GetDefinition_TenantAdminSelfGrant_Returns404Then200()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (projectId, definitionId) = await CreateOwnedDefinitionAsync(suffix);
        var username = $"tadmin-{suffix}";
        var principalId = await _seeder.SeedUserInTenantAsync(
            ScenarioTenantIds.DefaultTenantId,
            username,
            "Password1!",
            isTenantAdmin: true,
            permissionKeys: [WellKnownPermissionKeys.DefinitionsRead]);
        var login = await _http.LoginAsync("default", username, "Password1!");
        using var client = _http.CreateAuthenticatedClient(login.AccessToken, "default");
        var definitionUri = new Uri($"/v1/definitions/{Uri.EscapeDataString(definitionId)}", UriKind.Relative);

        // Act
        var before = await client.GetAsync(definitionUri);
        var grant = await client.PutAsJsonAsync(
            $"/v1/projects/{projectId}/members/{principalId}",
            new { role = "admin" });
        var after = await client.GetAsync(definitionUri);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    /// <summary>
    /// <c>executions.write</c> があっても、実行の定義の Project に未所属なら Cancel は 404 になる。
    /// 実行 ID を知っていても他 Project の実行を止められないことを固定する。
    /// </summary>
    [SkippableFact]
    public async Task CancelExecution_SameTenantNonMember_Returns404()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (_, executionId) = await StartOwnedExecutionAsync(suffix);
        var username = $"nomember-cancel-{suffix}";
        await _seeder.SeedUserInTenantAsync(
            ScenarioTenantIds.DefaultTenantId,
            username,
            "Password1!",
            isTenantAdmin: false,
            permissionKeys: [WellKnownPermissionKeys.ExecutionsWrite]);
        var login = await _http.LoginAsync("default", username, "Password1!");
        using var client = _http.CreateAuthenticatedClient(login.AccessToken, "default");

        // Act
        var response = await client.PostAsJsonAsync($"/v1/executions/{executionId}/cancel", new { });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// <c>executions.write</c> があっても、実行の定義の Project に未所属ならイベント送信は 404 になる。
    /// 子の待機が 2 件で届け先が決まらない実行でも、422 で実行の存在と子の待機の件数を見せないことを固定する。
    /// </summary>
    [SkippableFact]
    public async Task PublishEvent_SameTenantNonMember_WhenChildWaitsAmbiguous_Returns404()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (_, executionId) = await StartOwnedExecutionAsync(
            suffix,
            WorkflowYamlFixtures.ForkTwoWaitsSameEventWorkflow);
        var ownerLogin = await _http.LoginAsync("default", $"owner-{suffix}", "Password1!");
        using var ownerClient = _http.CreateAuthenticatedClient(ownerLogin.AccessToken, "default");
        await ExecutionSettleProbe.WaitUntilAsync(
            ownerClient,
            executionId,
            snapshot => snapshot.Waits.Count == 2,
            "子の待機が 2 件");
        var username = $"nomember-event-{suffix}";
        await _seeder.SeedUserInTenantAsync(
            ScenarioTenantIds.DefaultTenantId,
            username,
            "Password1!",
            isTenantAdmin: false,
            permissionKeys: [WellKnownPermissionKeys.ExecutionsWrite]);
        var login = await _http.LoginAsync("default", username, "Password1!");
        using var client = _http.CreateAuthenticatedClient(login.AccessToken, "default");

        // Act
        var response = await client.PostAsJsonAsync($"/v1/executions/{executionId}/events", new { name = "Go" });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Executor でも実行リソース許可が別の定義だけなら、イベント送信は 403 <c>RESOURCE_GRANT_DENIED</c> になり、待機は残る。
    /// イベント送信は再開の互換シムなので、再開・取消と同じ名簿で止めることを固定する。
    /// </summary>
    [SkippableFact]
    public async Task PublishEvent_ExecutorOutsideResourceGrant_Returns403AndKeepsWait()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (projectId, executionId) = await StartOwnedExecutionAsync(suffix);
        var ownerLogin = await _http.LoginAsync("default", $"owner-{suffix}", "Password1!");
        using var ownerClient = _http.CreateAuthenticatedClient(ownerLogin.AccessToken, "default");
        await ExecutionSettleProbe.WaitUntilAsync(
            ownerClient,
            executionId,
            snapshot => snapshot.Waits.Count == 1,
            "待機が 1 件");
        var username = $"outside-grant-{suffix}";
        var principalId = await _seeder.SeedUserInTenantAsync(
            ScenarioTenantIds.DefaultTenantId,
            username,
            "Password1!",
            isTenantAdmin: false,
            permissionKeys: [WellKnownPermissionKeys.DefinitionsRead, WellKnownPermissionKeys.ExecutionsWrite]);
        await _seeder.GrantProjectRoleAsync(projectId, principalId, ProjectAccessRole.Executor);
        await _seeder.AddDefinitionResourceGrantAsync(ScenarioTenantIds.DefaultTenantId, principalId, Guid.NewGuid());
        var login = await _http.LoginAsync("default", username, "Password1!");
        using var client = _http.CreateAuthenticatedClient(login.AccessToken, "default");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/v1/executions/{executionId}/events",
            new { name = "ResumeEvt" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ScenarioHttpClient.AssertErrorCodeAsync(response, "RESOURCE_GRANT_DENIED");
        var after = await ExecutionSettleProbe.ObserveAsync(ownerClient, executionId);
        Assert.Single(after.Waits);
    }

    /// <summary>
    /// 2 つの Project の実行が同じ topic / key を待つとき、片方の所有者の集合配送は 204 で、自分の Project の待機だけを再開する。
    /// 所属しない Project の待機は、topic と key を知っていても再開できないことを固定する。
    /// </summary>
    [SkippableFact]
    public async Task PublishTopic_ResumesOnlyCallerProjectWaits()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffixA = Guid.NewGuid().ToString("N")[..8];
        var suffixB = Guid.NewGuid().ToString("N")[..8];
        var topic = $"scenario.ingress.{suffixA}";
        var key = $"k{suffixA}";
        var (_, executionA) = await StartOwnedExecutionAsync(suffixA, s => WorkflowYamlFixtures.SubscribeWorkflow(s, topic, key), separateProject: true);
        var (_, executionB) = await StartOwnedExecutionAsync(suffixB, s => WorkflowYamlFixtures.SubscribeWorkflow(s, topic, key), separateProject: true);
        var ownerALogin = await _http.LoginAsync("default", $"owner-{suffixA}", "Password1!");
        using var ownerAClient = _http.CreateAuthenticatedClient(ownerALogin.AccessToken, "default");
        var ownerBLogin = await _http.LoginAsync("default", $"owner-{suffixB}", "Password1!");
        using var ownerBClient = _http.CreateAuthenticatedClient(ownerBLogin.AccessToken, "default");
        await ExecutionSettleProbe.WaitUntilAsync(ownerAClient, executionA, snapshot => snapshot.Waits.Count == 1, "A の待機が 1 件");
        await ExecutionSettleProbe.WaitUntilAsync(ownerBClient, executionB, snapshot => snapshot.Waits.Count == 1, "B の待機が 1 件");

        // Act
        var response = await ownerAClient.PostAsJsonAsync("/v1/events", new { topic, key });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await ExecutionSettleProbe.WaitUntilAsync(ownerAClient, executionA, snapshot => snapshot.Waits.Count == 0, "A の待機が再開");
        var afterB = await ExecutionSettleProbe.ObserveAsync(ownerBClient, executionB);
        Assert.Single(afterB.Waits);
    }

    /// <summary>集合配送の候補に、呼び出し元が所属しない Project の実行の topic / key は出ない。</summary>
    [SkippableFact]
    public async Task ListEventSubscriptions_ExcludesOtherProjectPairs()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffixA = Guid.NewGuid().ToString("N")[..8];
        var suffixB = Guid.NewGuid().ToString("N")[..8];
        var topic = $"scenario.candidates.{suffixA}";
        var keyA = $"a{suffixA}";
        var keyB = $"b{suffixB}";
        var (_, executionA) = await StartOwnedExecutionAsync(suffixA, s => WorkflowYamlFixtures.SubscribeWorkflow(s, topic, keyA), separateProject: true);
        var (_, executionB) = await StartOwnedExecutionAsync(suffixB, s => WorkflowYamlFixtures.SubscribeWorkflow(s, topic, keyB), separateProject: true);
        var ownerALogin = await _http.LoginAsync("default", $"owner-{suffixA}", "Password1!");
        using var ownerAClient = _http.CreateAuthenticatedClient(ownerALogin.AccessToken, "default");
        var ownerBLogin = await _http.LoginAsync("default", $"owner-{suffixB}", "Password1!");
        using var ownerBClient = _http.CreateAuthenticatedClient(ownerBLogin.AccessToken, "default");
        await ExecutionSettleProbe.WaitUntilAsync(ownerAClient, executionA, snapshot => snapshot.Waits.Count == 1, "A の待機が 1 件");
        await ExecutionSettleProbe.WaitUntilAsync(ownerBClient, executionB, snapshot => snapshot.Waits.Count == 1, "B の待機が 1 件");

        // Act
        var response = await ownerAClient.GetAsync(new Uri("/v1/event-subscriptions", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var pairs = body.GetProperty("subscriptions")
            .EnumerateArray()
            .Select(item => (Topic: item.GetProperty("topic").GetString(), Key: item.GetProperty("key").GetString()))
            .ToList();
        Assert.Contains((topic, keyA), pairs);
        Assert.DoesNotContain((topic, keyB), pairs);
    }

    /// <summary>Reader は Start と同じく Cancel も 403 <c>PROJECT_ACCESS_DENIED</c> になる。</summary>
    [SkippableFact]
    public async Task CancelExecution_WithReaderAccess_Returns403ProjectAccessDenied()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (projectId, executionId) = await StartOwnedExecutionAsync(suffix);
        using var client = await CreateMemberClientAsync(suffix, projectId, ProjectAccessRole.Reader);

        // Act
        var response = await client.PostAsJsonAsync($"/v1/executions/{executionId}/cancel", new { });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await ScenarioHttpClient.AssertErrorCodeAsync(response, "PROJECT_ACCESS_DENIED");
    }

    /// <summary>オーナー（Project admin）が定義を作って Start し、Project と実行の表示 ID を返す。</summary>
    /// <remarks>定義は <paramref name="yamlFactory"/> で作る。省略時は <see cref="WorkflowYamlFixtures.WaitWorkflow"/>。</remarks>
    /// <param name="suffix">ユーザー名と定義名の一意サフィックス。</param>
    /// <param name="yamlFactory">定義 YAML を作る関数。</param>
    /// <param name="separateProject">true なら既定の Project ではなく、オーナーだけが所属する別の Project に定義を作る。</param>
    private async Task<(Guid ProjectId, string ExecutionDisplayId)> StartOwnedExecutionAsync(
        string suffix,
        Func<string, string>? yamlFactory = null,
        bool separateProject = false)
    {
        var (projectId, definitionId) = await CreateOwnedDefinitionAsync(suffix, yamlFactory, separateProject);
        var ownerLogin = await _http.LoginAsync("default", $"owner-{suffix}", "Password1!");
        using var ownerClient = _http.CreateAuthenticatedClient(ownerLogin.AccessToken, "default");
        var startResponse = await ownerClient.PostAsJsonAsync("/v1/executions", new { definitionId });
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        var execution = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        return (projectId, execution.GetProperty("displayId").GetString()!);
    }

    private async Task<(Guid ProjectId, string DefinitionDisplayId)> CreateOwnedDefinitionAsync(
        string suffix,
        Func<string, string>? yamlFactory = null,
        bool separateProject = false)
    {
        var ownerUsername = $"owner-{suffix}";
        var ownerPrincipalId = await _seeder.SeedUserAsync(ownerUsername, "Password1!");
        var projectId = separateProject
            ? await _seeder.SeedSeparateProjectWithAccessAsync(
                ScenarioTenantIds.DefaultTenantId,
                ownerPrincipalId,
                ProjectAccessRole.Admin)
            : await _seeder.SeedProjectWithAccessAsync(
                ScenarioTenantIds.DefaultTenantId,
                ownerPrincipalId,
                ProjectAccessRole.Admin);
        var ownerLogin = await _http.LoginAsync("default", ownerUsername, "Password1!");
        using var ownerClient = _http.CreateAuthenticatedClient(ownerLogin.AccessToken, "default");

        var createResponse = await ownerClient.PostAsJsonAsync("/v1/definitions", new
        {
            name = $"scenario-proj-{suffix}",
            yaml = (yamlFactory ?? WorkflowYamlFixtures.WaitWorkflow)(suffix),
            projectId
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var defJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        return (projectId, defJson.GetProperty("displayId").GetString()!);
    }

    private async Task<HttpClient> CreateMemberClientAsync(
        string suffix,
        Guid projectId,
        ProjectAccessRole role)
    {
        var username = $"member-{suffix}";
        var principalId = await _seeder.SeedUserInTenantAsync(
            ScenarioTenantIds.DefaultTenantId,
            username,
            "Password1!",
            isTenantAdmin: false,
            permissionKeys: [WellKnownPermissionKeys.DefinitionsRead, WellKnownPermissionKeys.ExecutionsWrite]);
        await _seeder.GrantProjectRoleAsync(projectId, principalId, role);
        var login = await _http.LoginAsync("default", username, "Password1!");
        return _http.CreateAuthenticatedClient(login.AccessToken, "default");
    }
}
