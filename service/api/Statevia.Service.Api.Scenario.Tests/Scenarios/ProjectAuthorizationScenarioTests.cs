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
            status is "Running" or "Queued",
            $"Expected Running or Queued, got: {status}");
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
    private async Task<(Guid ProjectId, string ExecutionDisplayId)> StartOwnedExecutionAsync(string suffix)
    {
        var (projectId, definitionId) = await CreateOwnedDefinitionAsync(suffix);
        var ownerLogin = await _http.LoginAsync("default", $"owner-{suffix}", "Password1!");
        using var ownerClient = _http.CreateAuthenticatedClient(ownerLogin.AccessToken, "default");
        var startResponse = await ownerClient.PostAsJsonAsync("/v1/executions", new { definitionId });
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        var execution = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        return (projectId, execution.GetProperty("displayId").GetString()!);
    }

    private async Task<(Guid ProjectId, string DefinitionDisplayId)> CreateOwnedDefinitionAsync(string suffix)
    {
        var ownerUsername = $"owner-{suffix}";
        var ownerPrincipalId = await _seeder.SeedUserAsync(ownerUsername, "Password1!");
        var projectId = await _seeder.SeedProjectWithAccessAsync(
            ScenarioTenantIds.DefaultTenantId,
            ownerPrincipalId,
            ProjectAccessRole.Admin);
        var ownerLogin = await _http.LoginAsync("default", ownerUsername, "Password1!");
        using var ownerClient = _http.CreateAuthenticatedClient(ownerLogin.AccessToken, "default");

        var createResponse = await ownerClient.PostAsJsonAsync("/v1/definitions", new
        {
            name = $"scenario-proj-{suffix}",
            yaml = WorkflowYamlFixtures.WaitWorkflow(suffix),
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
