using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Statevia.Service.Api.Scenario.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Statevia.Service.Api.Scenario.Tests.Scenarios;

/// <summary>
/// 一覧 API の拡張（待機の絞り込み・件数・最後の実行・並び順）を PostgreSQL と HTTP で確かめるシナリオテスト。
/// </summary>
/// <remarks>
/// <para>テストごとに専用のテナントを作り、定義・実行・Wait・物理子の関係を DB へ直接投入する（Worker を回さない）。</para>
/// <list type="bullet">
///   <item><c>waiting=true</c> は自身または子孫の物理子 Execution が EventWait を持つルートの実行だけ。DelayWait だけの実行は含めない</item>
///   <item><c>/v1/executions/counts</c> は状態ごと・待機中・要対応を数え、メンバーでない Project の実行を数えない</item>
///   <item>定義の一覧は各要素に <c>lastExecution</c> を付け、実行の無い定義は <c>null</c></item>
///   <item>許可していない <c>sortBy</c> は 422</item>
/// </list>
/// </remarks>
[Trait("Category", "Scenario")]
[Collection(nameof(PostgresScenarioCollection))]
public sealed class ExecutionListExtensionsScenarioTests : ScenarioTestBase
{
    private const string Password = "Password1!";
    private static readonly DateTime Day1 = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ScenarioDatabaseSeeder _seeder;
    private readonly ScenarioHttpClient _http;

    /// <summary>テストクラスを初期化する。</summary>
    /// <param name="fixture">PostgreSQL コンテナフィクスチャ。</param>
    /// <param name="output">xUnit テスト出力ヘルパー。</param>
    public ExecutionListExtensionsScenarioTests(PostgresScenarioFixture fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _seeder = new ScenarioDatabaseSeeder(fixture.ConnectionString);
        _http = new ScenarioHttpClient(fixture.Factory!);
    }

    /// <summary>waiting=true は EventWait を持つルートと、子孫に EventWait を持つ親だけを返し、各要素の waiting は true。</summary>
    [SkippableFact]
    public async Task ListExecutions_WhenWaitingTrue_ReturnsWaitingRootsOnly()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var world = await SeedWorldAsync();
        using var client = await LoginAsync(world);

        // Act
        var response = await client.GetAsync(new Uri("/v1/executions?limit=50&waiting=true", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetProperty("totalCount").GetInt32());
        var resourceIds = body.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("resourceId").GetGuid())
            .Order()
            .ToList();
        Assert.Equal(new[] { world.EventWaiting, world.ForkParent }.Order(), resourceIds);
        Assert.All(body.RootElement.GetProperty("items").EnumerateArray(), item => Assert.True(item.GetProperty("waiting").GetBoolean()));
    }

    /// <summary>件数は状態ごと・待機中・要対応を数え、メンバーでない Project の実行と物理子を数えない。</summary>
    [SkippableFact]
    public async Task CountExecutions_ReturnsCountsWithinAccessibleRoots()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var world = await SeedWorldAsync();
        using var client = await LoginAsync(world);

        // Act
        var response = await client.GetAsync(new Uri("/v1/executions/counts", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(5, root.GetProperty("total").GetInt32());
        Assert.Equal(4, root.GetProperty("byStatus").GetProperty("ACTIVE").GetInt32());
        Assert.Equal(1, root.GetProperty("byStatus").GetProperty("FAILED").GetInt32());
        Assert.Equal(0, root.GetProperty("byStatus").GetProperty("UNKNOWN").GetInt32());
        Assert.Equal(2, root.GetProperty("waiting").GetInt32());
        Assert.Equal(3, root.GetProperty("needsAttention").GetInt32());
    }

    /// <summary>定義の一覧は各要素に lastExecution を付け、実行の無い定義は null にする。</summary>
    [SkippableFact]
    public async Task ListDefinitions_AttachesLastExecution()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var world = await SeedWorldAsync();
        using var client = await LoginAsync(world);

        // Act
        var response = await client.GetAsync(new Uri("/v1/definitions?limit=50&sortBy=updatedAt", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
        var withExecutions = items.Single(item => item.GetProperty("resourceId").GetGuid() == world.DefinitionId);
        var lastExecution = withExecutions.GetProperty("lastExecution");
        Assert.Equal(world.LatestRoot, Guid.Parse(lastExecution.GetProperty("displayId").GetString()!));
        Assert.Equal("FAILED", lastExecution.GetProperty("status").GetString());
        var empty = items.Single(item => item.GetProperty("resourceId").GetGuid() == world.EmptyDefinitionId);
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("lastExecution").ValueKind);
    }

    /// <summary>許可していない sortBy は 422。</summary>
    [SkippableFact]
    public async Task ListExecutions_WhenSortByIsNotAllowed_Returns422()
    {
        // Arrange
        SkipIfDockerUnavailable();
        var world = await SeedWorldAsync();
        using var client = await LoginAsync(world);

        // Act
        var response = await client.GetAsync(new Uri("/v1/executions?limit=10&sortBy=createdAt", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await ScenarioHttpClient.AssertErrorCodeAsync(response, "VALIDATION_ERROR");
    }

    private sealed record World(
        string TenantKey,
        string Username,
        Guid DefinitionId,
        Guid EmptyDefinitionId,
        Guid EventWaiting,
        Guid ForkParent,
        Guid LatestRoot);

    private async Task<HttpClient> LoginAsync(World world)
    {
        var login = await _http.LoginAsync(world.TenantKey, world.Username, Password);
        return _http.CreateAuthenticatedClient(login.AccessToken, world.TenantKey);
    }

    /// <summary>
    /// 専用テナントに、メンバーの Project（定義 2 つ）とメンバーでない Project（定義 1 つ）を作り、実行を投入する。
    /// </summary>
    /// <remarks>
    /// メンバーの定義の実行: EventWait（Running）、待機なし（Running）、DelayWait だけ（Running）、子に EventWait を持つ親（Running）と子、
    /// 最後に始まった失敗（Failed）。メンバーでない定義の実行: EventWait（Running）。表示 ID は付けない（応答は UUID の文字列になる）。
    /// </remarks>
    private async Task<World> SeedWorldAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tenantId = await _seeder.SeedTenantAsync(suffix);
        var username = $"lister-{suffix}";
        var principalId = await _seeder.SeedUserInTenantAsync(
            tenantId,
            username,
            Password,
            isTenantAdmin: false,
            permissionKeys: [WellKnownPermissionKeys.DefinitionsRead, WellKnownPermissionKeys.ExecutionsRead]);

        await using var db = new CoreDbContext(
            new DbContextOptionsBuilder<CoreDbContext>().UseNpgsql(Fixture.ConnectionString).Options);
        var memberProjectId = AddProject(db, tenantId, "member");
        var otherProjectId = AddProject(db, tenantId, "other");
        db.ProjectAccesses.Add(new ProjectAccessRow
        {
            ProjectId = memberProjectId,
            PrincipalId = principalId,
            Role = ProjectAccessRole.Reader,
            CreatedAt = DateTime.UtcNow
        });

        var (definitionId, versionId) = AddDefinition(db, tenantId, memberProjectId, "with-executions");
        var (emptyDefinitionId, _) = AddDefinition(db, tenantId, memberProjectId, "without-executions");
        var (otherDefinitionId, otherVersionId) = AddDefinition(db, tenantId, otherProjectId, "other");

        var eventWaiting = AddExecution(db, tenantId, definitionId, versionId, "Running", Day1);
        AddExecution(db, tenantId, definitionId, versionId, "Running", Day1);
        var delayWaiting = AddExecution(db, tenantId, definitionId, versionId, "Running", Day1);
        var forkParent = AddExecution(db, tenantId, definitionId, versionId, "Running", Day1);
        var forkChild = AddExecution(db, tenantId, definitionId, versionId, "Running", Day1.AddDays(5));
        var latestRoot = AddExecution(db, tenantId, definitionId, versionId, "Failed", Day1.AddDays(3));
        var otherWaiting = AddExecution(db, tenantId, otherDefinitionId, otherVersionId, "Running", Day1);
        await db.SaveChangesAsync();

        AddWait(db, eventWaiting, ExecutionWaitKind.EventWait);
        AddWait(db, delayWaiting, ExecutionWaitKind.DelayWait);
        AddWait(db, forkChild, ExecutionWaitKind.EventWait);
        AddWait(db, otherWaiting, ExecutionWaitKind.EventWait);
        db.ExecutionBranches.Add(new ExecutionBranchRow
        {
            ParentExecutionId = forkParent,
            ExecutionId = forkChild,
            ForkNodeId = "fork-1",
            JoinState = "join",
            BranchState = "branch",
            Status = ExecutionBranchStatuses.Running,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return new World($"scenario-{suffix}", username, definitionId, emptyDefinitionId, eventWaiting, forkParent, latestRoot);
    }

    private static Guid AddProject(CoreDbContext db, Guid tenantId, string slug)
    {
        var projectId = Guid.NewGuid();
        db.Projects.Add(new ProjectRow
        {
            ProjectId = projectId,
            TenantId = tenantId,
            Slug = slug,
            DisplayName = slug,
            CreatedAt = DateTime.UtcNow
        });
        return projectId;
    }

    private static (Guid DefinitionId, Guid VersionId) AddDefinition(CoreDbContext db, Guid tenantId, Guid projectId, string name)
    {
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Definitions.Add(new DefinitionRow
        {
            DefinitionId = definitionId,
            TenantId = tenantId,
            ProjectId = projectId,
            Slug = DefinitionSlug.FromName(definitionId, name),
            Name = name,
            LatestVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.DefinitionVersions.Add(new DefinitionVersionRow
        {
            DefinitionVersionId = versionId,
            DefinitionId = definitionId,
            Version = 1,
            SourceYaml = "x",
            CompiledJson = "{}",
            CreatedAt = now
        });
        return (definitionId, versionId);
    }

    private static Guid AddExecution(
        CoreDbContext db,
        Guid tenantId,
        Guid definitionId,
        Guid versionId,
        string status,
        DateTime startedAt)
    {
        var executionId = Guid.NewGuid();
        db.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = tenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = versionId,
            Status = status,
            StartedAt = startedAt,
            UpdatedAt = startedAt,
            CancelRequested = false,
            RestartLost = false
        });
        return executionId;
    }

    private static void AddWait(CoreDbContext db, Guid executionId, ExecutionWaitKind waitKind) =>
        db.ExecutionWaits.Add(new ExecutionWaitRow
        {
            ExecutionId = executionId,
            NodeId = "wait-1",
            WaitKind = waitKind,
            AllowedEvents = ["approve"],
            CreatedAt = DateTime.UtcNow
        });
}
