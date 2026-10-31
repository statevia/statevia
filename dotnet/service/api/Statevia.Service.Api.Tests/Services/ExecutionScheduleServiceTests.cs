using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Services;
using Statevia.Infrastructure.Persistence.Repositories;
using Statevia.Service.Api.Tests.Infrastructure;
using System.Text.Json;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ExecutionScheduleService"/> の CRUD と認可境界。</summary>
public sealed class ExecutionScheduleServiceTests
{
    /// <summary>作成で next_fire_at が UTC の未来になり、定義 YAML は変わらない。</summary>
    [Fact]
    public async Task CreateAsync_PersistsNextFireWithoutChangingYaml()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        var caller = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, sourceYaml: "states: []", executorMembers: [runAs]);
        var sut = CreateSut(db, caller, runAs);

        // Act
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, created.ScheduleId);
        Assert.Equal(DateTimeKind.Utc, created.NextFireAt.Kind);
        Assert.True(created.NextFireAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal(caller, created.CreatedByPrincipalId);
        Assert.Null(created.DefinitionVersionId);
        await using var verify = db.Factory.CreateDbContext();
        Assert.Equal("states: []", (await verify.DefinitionVersions.SingleAsync()).SourceYaml);
        Assert.Equal(0, await verify.ServiceAccounts.CountAsync());
    }

    /// <summary>一覧は input を持たず、単票は input を返す。</summary>
    [Fact]
    public async Task ListAsync_OmitsInput_GetAsync_IncludesInput()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        var caller = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs, caller]);
        var sut = CreateSut(db, caller, runAs);
        var request = NewCreateRequest(definitionId, runAs);
        request.Input = JsonDocument.Parse("""{"k":1}""").RootElement.Clone();
        var created = await sut.CreateAsync(request, CancellationToken.None);

        // Act
        var list = await sut.ListAsync(CancellationToken.None);
        var detail = await sut.GetAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        Assert.Single(list);
        Assert.IsType<ExecutionScheduleListItemDto>(list[0]);
        Assert.Equal(JsonValueKind.Object, detail.Input?.ValueKind);
        Assert.Equal(1, detail.Input?.GetProperty("k").GetInt32());
    }

    /// <summary>User を run-as にすると 422。</summary>
    [Fact]
    public async Task CreateAsync_WhenRunAsUser_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var userPrincipal = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [userPrincipal]);
        var principals = new StubPrincipalDataAccess();
        principals.Add(userPrincipal, TestTenantIds.DefaultTenantId, PrincipalType.User, isActive: true);
        var sut = CreateSut(db, Guid.NewGuid(), userPrincipal, principals);

        // Act
        var act = () => sut.CreateAsync(NewCreateRequest(definitionId, userPrincipal), CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Contains("runAsPrincipalId", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>run-as の許可に無い定義ではスケジュールを保存しない。</summary>
    [Fact]
    public async Task CreateAsync_WhenRunAsLacksGrant_ThrowsForbidden()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var grants = new MutableGrantStore();
        grants.Rows.Add(Grant(runAs, PrincipalResourceGrantKinds.Definition, Guid.NewGuid()));
        var sut = CreateSut(db, Guid.NewGuid(), runAs, resourceGrants: grants);

        // Act
        var act = () => sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<ForbiddenException>(act);
        Assert.Equal("RESOURCE_GRANT_DENIED", error.Code);
        await using var verify = db.Factory.CreateDbContext();
        Assert.Empty(verify.ExecutionSchedules.IgnoreQueryFilters());
    }

    /// <summary>割り当て後に許可を狭めると、定義を変えない更新も拒否する。</summary>
    [Fact]
    public async Task UpdateAsync_WhenGrantNarrowed_ThrowsForbidden()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var grants = new MutableGrantStore();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, resourceGrants: grants);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        grants.Rows.Add(Grant(runAs, PrincipalResourceGrantKinds.Definition, Guid.NewGuid()));

        // Act
        var act = () => sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Name = "renamed-job" },
            CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<ForbiddenException>(act);
        Assert.Equal("RESOURCE_GRANT_DENIED", error.Code);
        var stored = await sut.GetAsync(created.ScheduleId, CancellationToken.None);
        Assert.Equal("nightly-job", stored.Name);
    }

    /// <summary>不正 cron は 422 で field=cronExpression。</summary>
    [Fact]
    public async Task CreateAsync_WhenCronInvalid_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var request = NewCreateRequest(definitionId, runAs);
        request.CronExpression = "not-a-cron";

        // Act
        var act = () => sut.CreateAsync(request, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("cronExpression", ReadField(ex));
    }

    /// <summary>不明 TZ は 422 で field=timeZone。</summary>
    [Fact]
    public async Task CreateAsync_WhenTimeZoneUnknown_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var request = NewCreateRequest(definitionId, runAs);
        request.TimeZone = "Not/AZone";

        // Act
        var act = () => sut.CreateAsync(request, CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("timeZone", ReadField(ex));
    }

    /// <summary>存在しない版指定は 404。</summary>
    [Fact]
    public async Task CreateAsync_WhenVersionMissing_ThrowsNotFound()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var request = NewCreateRequest(definitionId, runAs);
        request.DefinitionVersion = 99;

        // Act
        var act = () => sut.CreateAsync(request, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    /// <summary>他テナントのスケジュールは 404。</summary>
    [Fact]
    public async Task GetAsync_WhenOtherTenantRow_ThrowsNotFound()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, tenantId: TestTenantIds.T1TenantId, executorMembers: [runAs]);
        var otherSut = CreateSut(
            db,
            Guid.NewGuid(),
            runAs,
            tenant: TestTenantIds.T1Context with { PrincipalId = Guid.NewGuid() },
            runAsTenantId: TestTenantIds.T1TenantId);
        var created = await otherSut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        var defaultSut = CreateSut(db, Guid.NewGuid(), runAs);

        // Act
        var act = () => defaultSut.GetAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    /// <summary>enabled=false にするとスケジュールは残るが Dispatcher 対象外。</summary>
    [Fact]
    public async Task UpdateAsync_WhenDisabled_PersistsEnabledFalse()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Act
        var updated = await sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Enabled = false },
            CancellationToken.None);

        // Assert
        Assert.False(updated.Enabled);
        Assert.Equal(created.NextFireAt, updated.NextFireAt);
    }

    /// <summary>論理削除後は取得できない。</summary>
    [Fact]
    public async Task DeleteAsync_HidesScheduleFromGet()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Act
        await sut.DeleteAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetAsync(created.ScheduleId, CancellationToken.None));
    }

    /// <summary>手動実行は Dispatcher に委譲し、cron の next は変えない。</summary>
    [Fact]
    public async Task RunNowAsync_WhenEnabled_DelegatesWithoutAdvancingNextFire()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var dispatch = new RecordingDispatchService();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, dispatch: dispatch);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Act
        var started = await sut.RunNowAsync(created.ScheduleId, "manual-1", CancellationToken.None);

        // Assert
        Assert.Equal(dispatch.StartedExecutionId, started.ResourceId);
        Assert.Equal(1, dispatch.RunCount);
        Assert.Equal("manual-1", dispatch.LastIdempotencyKey);
        var detail = await sut.GetAsync(created.ScheduleId, CancellationToken.None);
        Assert.Equal(created.NextFireAt, detail.NextFireAt);
    }

    /// <summary>無効スケジュールの手動実行は 422 で Start しない。</summary>
    [Fact]
    public async Task RunNowAsync_WhenDisabled_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var dispatch = new RecordingDispatchService();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, dispatch: dispatch);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Enabled = false },
            CancellationToken.None);

        // Act
        var act = () => sut.RunNowAsync(created.ScheduleId, "manual-1", CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("enabled", ReadField(ex));
        Assert.Equal(0, dispatch.RunCount);
    }

    /// <summary>論理削除後の手動実行は 404。</summary>
    [Fact]
    public async Task RunNowAsync_WhenDeleted_ThrowsNotFound()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var dispatch = new RecordingDispatchService();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, dispatch: dispatch);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await sut.DeleteAsync(created.ScheduleId, CancellationToken.None);

        // Act
        var act = () => sut.RunNowAsync(created.ScheduleId, "manual-1", CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Equal(0, dispatch.RunCount);
    }

    /// <summary>同一テナントの重複名は 422。</summary>
    [Fact]
    public async Task CreateAsync_WhenNameDuplicated_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Act
        var act = () => sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("name", ReadField(ex));
    }

    /// <summary>run-as が定義の Project に所属していなければ、作成は 422 で保存しない。</summary>
    [Fact]
    public async Task CreateAsync_WhenRunAsNotMember_ThrowsValidationWithoutSaving()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);

        // Act
        var act = () => sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("runAsPrincipalId", ReadField(ex));
        await using var verify = db.Factory.CreateDbContext();
        Assert.Empty(verify.ExecutionSchedules.IgnoreQueryFilters());
    }

    /// <summary>run-as が reader の作成は 422。executor 以上が要る。</summary>
    [Fact]
    public async Task CreateAsync_WhenRunAsReader_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        var projectId = await SeedDefinitionAsync(db, definitionId);
        await GrantAccessAsync(db, projectId, runAs, ProjectAccessRole.Reader);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);

        // Act
        var act = () => sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("runAsPrincipalId", ReadField(ex));
    }

    /// <summary>無効のまま作成するなら run-as の所属は確かめない。有効にするときに確かめる。</summary>
    [Fact]
    public async Task CreateAsync_WhenDisabledAndRunAsNotMember_Persists()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var request = NewCreateRequest(definitionId, runAs);
        request.Enabled = false;

        // Act
        var created = await sut.CreateAsync(request, CancellationToken.None);

        // Assert
        Assert.False(created.Enabled);
    }

    /// <summary>run-as の所属が外れた有効なスケジュールも、無効化は通す。</summary>
    [Fact]
    public async Task UpdateAsync_WhenRunAsLeftProjectAndDisabling_Persists()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await RemoveAccessAsync(db, runAs);

        // Act
        var updated = await sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Enabled = false },
            CancellationToken.None);

        // Assert
        Assert.False(updated.Enabled);
    }

    /// <summary>run-as の所属が外れた有効なスケジュールは、無効化以外の更新を 422 で止める。</summary>
    [Fact]
    public async Task UpdateAsync_WhenRunAsLeftProjectAndRenamingEnabledRow_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await RemoveAccessAsync(db, runAs);

        // Act
        var act = () => sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Name = "renamed-job" },
            CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("runAsPrincipalId", ReadField(ex));
        var stored = await sut.GetAsync(created.ScheduleId, CancellationToken.None);
        Assert.Equal("nightly-job", stored.Name);
    }

    /// <summary>run-as の所属が外れた無効なスケジュールは、名前を変えられる。</summary>
    [Fact]
    public async Task UpdateAsync_WhenRunAsLeftProjectAndRenamingDisabledRow_Persists()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await sut.UpdateAsync(created.ScheduleId, new UpdateExecutionScheduleRequest { Enabled = false }, CancellationToken.None);
        await RemoveAccessAsync(db, runAs);

        // Act
        var updated = await sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Name = "renamed-job" },
            CancellationToken.None);

        // Assert
        Assert.Equal("renamed-job", updated.Name);
    }

    /// <summary>run-as の所属が外れた無効なスケジュールを有効にすると 422。</summary>
    [Fact]
    public async Task UpdateAsync_WhenRunAsLeftProjectAndEnabling_ThrowsValidation()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await sut.UpdateAsync(created.ScheduleId, new UpdateExecutionScheduleRequest { Enabled = false }, CancellationToken.None);
        await RemoveAccessAsync(db, runAs);

        // Act
        var act = () => sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Enabled = true },
            CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("runAsPrincipalId", ReadField(ex));
    }

    /// <summary>run-as の許可を狭めたスケジュールも、無効化は通す。</summary>
    [Fact]
    public async Task UpdateAsync_WhenGrantNarrowedAndDisabling_Persists()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var grants = new MutableGrantStore();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, resourceGrants: grants);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        grants.Rows.Add(Grant(runAs, PrincipalResourceGrantKinds.Definition, Guid.NewGuid()));

        // Act
        var updated = await sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Enabled = false },
            CancellationToken.None);

        // Assert
        Assert.False(updated.Enabled);
    }

    /// <summary>手動実行は run-as が未所属なら 422 で、Start しない。</summary>
    [Fact]
    public async Task RunNowAsync_WhenRunAsNotMember_ThrowsValidationWithoutStart()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var dispatch = new RecordingDispatchService();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, dispatch: dispatch);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await RemoveAccessAsync(db, runAs);

        // Act
        var act = () => sut.RunNowAsync(created.ScheduleId, "manual-1", CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("runAsPrincipalId", ReadField(ex));
        Assert.Equal(0, dispatch.RunCount);
    }

    /// <summary>手動実行は run-as の許可の外なら 403 で、Start しない。</summary>
    [Fact]
    public async Task RunNowAsync_WhenRunAsLacksGrant_ThrowsForbiddenWithoutStart()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var grants = new MutableGrantStore();
        var dispatch = new RecordingDispatchService();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, dispatch: dispatch, resourceGrants: grants);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        grants.Rows.Add(Grant(runAs, PrincipalResourceGrantKinds.Definition, Guid.NewGuid()));

        // Act
        var act = () => sut.RunNowAsync(created.ScheduleId, "manual-1", CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<ForbiddenException>(act);
        Assert.Equal("RESOURCE_GRANT_DENIED", error.Code);
        Assert.Equal(0, dispatch.RunCount);
    }

    /// <summary>定義が論理削除されたスケジュールの手動実行は 422（field=definitionId）。</summary>
    [Fact]
    public async Task RunNowAsync_WhenDefinitionDeleted_ThrowsValidationOnDefinitionId()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var dispatch = new RecordingDispatchService();
        var sut = CreateSut(db, Guid.NewGuid(), runAs, dispatch: dispatch);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await DeleteDefinitionAsync(db, definitionId);

        // Act
        var act = () => sut.RunNowAsync(created.ScheduleId, "manual-1", CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("definitionId", ReadField(ex));
        Assert.Equal(0, dispatch.RunCount);
    }

    /// <summary>定義が論理削除されたスケジュールも無効化できる。</summary>
    [Fact]
    public async Task UpdateAsync_WhenDefinitionDeletedAndDisabling_Persists()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await DeleteDefinitionAsync(db, definitionId);

        // Act
        var updated = await sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Enabled = false },
            CancellationToken.None);

        // Assert
        Assert.False(updated.Enabled);
    }

    /// <summary>定義が論理削除されたスケジュールも削除できる。</summary>
    [Fact]
    public async Task DeleteAsync_WhenDefinitionDeleted_Deletes()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await DeleteDefinitionAsync(db, definitionId);

        // Act
        await sut.DeleteAsync(created.ScheduleId, CancellationToken.None);

        // Assert
        await using var verify = db.Factory.CreateDbContext();
        var stored = await verify.ExecutionSchedules.IgnoreQueryFilters().SingleAsync();
        Assert.NotNull(stored.DeletedAt);
    }

    /// <summary>定義が論理削除されたスケジュールを有効にすると 422（field=definitionId）。</summary>
    [Fact]
    public async Task UpdateAsync_WhenDefinitionDeletedAndEnabling_ThrowsValidationOnDefinitionId()
    {
        // Arrange
        using var db = new SqliteTestDatabase();
        var definitionId = Guid.NewGuid();
        var runAs = Guid.NewGuid();
        await SeedDefinitionAsync(db, definitionId, executorMembers: [runAs]);
        var sut = CreateSut(db, Guid.NewGuid(), runAs);
        var created = await sut.CreateAsync(NewCreateRequest(definitionId, runAs), CancellationToken.None);
        await sut.UpdateAsync(created.ScheduleId, new UpdateExecutionScheduleRequest { Enabled = false }, CancellationToken.None);
        await DeleteDefinitionAsync(db, definitionId);

        // Act
        var act = () => sut.UpdateAsync(
            created.ScheduleId,
            new UpdateExecutionScheduleRequest { Enabled = true },
            CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<ApiValidationException>(act);
        Assert.Equal("definitionId", ReadField(ex));
    }

    private static CreateExecutionScheduleRequest NewCreateRequest(Guid definitionId, Guid runAs) =>
        new()
        {
            Name = "nightly-job",
            DefinitionId = definitionId.ToString("D"),
            CronExpression = "0 3 * * *",
            TimeZone = "Asia/Tokyo",
            RunAsPrincipalId = runAs
        };

    private static string? ReadField(ApiValidationException exception)
    {
        var field = exception.Details?.GetType().GetProperty("field")?.GetValue(exception.Details);
        return field as string;
    }

    /// <summary>Project と定義を作り、指定 Principal を executor でその Project のメンバーにする。</summary>
    /// <returns>作成した Project ID。</returns>
    private static async Task<Guid> SeedDefinitionAsync(
        SqliteTestDatabase db,
        Guid definitionId,
        string sourceYaml = "x",
        Guid? tenantId = null,
        Guid[]? executorMembers = null)
    {
        var resolvedTenant = tenantId ?? TestTenantIds.DefaultTenantId;
        var tenantKey = resolvedTenant == TestTenantIds.T1TenantId ? "t1" : "default";
        await using var ctx = db.Factory.CreateDbContext();
        var projectId = Guid.NewGuid();
        ProjectTestData.AddDefaultProject(ctx, resolvedTenant, tenantKey, projectId);
        DefinitionTestData.AddDefinitionWithVersion(
            ctx,
            resolvedTenant,
            definitionId,
            "wf-schedule",
            projectId,
            sourceYaml: sourceYaml);
        foreach (var principalId in executorMembers ?? [])
        {
            ProjectTestData.AddPrincipal(ctx, resolvedTenant, principalId);
            ProjectTestData.GrantAccess(ctx, projectId, principalId, ProjectAccessRole.Executor);
        }
        await ctx.SaveChangesAsync();
        return projectId;
    }

    /// <summary>既定テナントに Principal を作り、指定ロールで Project のメンバーにする。</summary>
    private static async Task GrantAccessAsync(
        SqliteTestDatabase db,
        Guid projectId,
        Guid principalId,
        ProjectAccessRole role)
    {
        await using var ctx = db.Factory.CreateDbContext();
        ProjectTestData.AddPrincipal(ctx, TestTenantIds.DefaultTenantId, principalId);
        ProjectTestData.GrantAccess(ctx, projectId, principalId, role);
        await ctx.SaveChangesAsync();
    }

    /// <summary>Principal の Project 所属をすべて外す。</summary>
    private static async Task RemoveAccessAsync(SqliteTestDatabase db, Guid principalId)
    {
        await using var ctx = db.Factory.CreateDbContext();
        ctx.ProjectAccesses.RemoveRange(ctx.ProjectAccesses.Where(access => access.PrincipalId == principalId));
        await ctx.SaveChangesAsync();
    }

    /// <summary>定義を論理削除する。</summary>
    private static async Task DeleteDefinitionAsync(SqliteTestDatabase db, Guid definitionId)
    {
        await using var ctx = db.Factory.CreateDbContext();
        var definition = await ctx.Definitions.IgnoreQueryFilters().SingleAsync(row => row.DefinitionId == definitionId);
        definition.DeletedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    private static ExecutionScheduleService CreateSut(
        SqliteTestDatabase db,
        Guid callerPrincipalId,
        Guid runAsPrincipalId,
        StubPrincipalDataAccess? principals = null,
        TenantContextState? tenant = null,
        Guid? runAsTenantId = null,
        IExecutionScheduleDispatchService? dispatch = null,
        IPrincipalResourceGrantStore? resourceGrants = null)
    {
        var tenantState = tenant ?? TestTenantIds.DefaultContext with { PrincipalId = callerPrincipalId };
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(tenantState);
        var principalAccess = principals ?? new StubPrincipalDataAccess();
        if (principals is null)
        {
            principalAccess.Add(
                runAsPrincipalId,
                runAsTenantId ?? tenantState.TenantId,
                PrincipalType.ServiceAccount,
                isActive: true);
        }

        var uowFactory = new TestCoreUnitOfWorkFactory(db.Factory);
        var guard = new ExecutionAuthorizationGuard(
            new AllowAllRuntimePermissionAuthorization(),
            new AllowAllExecutionMutationAuthorization(),
            new AllowAllProjectAuthorizationService(),
            TestRepositoryFactory.CreateDefinitionRepository(),
            new TestCoreTransactionExecutor(uowFactory),
            new PrincipalResourceGrantAuthorization(
                resourceGrants ?? new EmptyPrincipalResourceGrantStore(),
                accessor,
                principalAccess),
            accessor);

        var transactionExecutor = new TestCoreTransactionExecutor(uowFactory);
        return new ExecutionScheduleService(
            new ExecutionScheduleRepository(db.Factory),
            guard,
            new ExecutionScheduleDefinitionResolver(
                new GuidParsingDisplayIdService(),
                TestRepositoryFactory.CreateDefinitionRepository(),
                transactionExecutor,
                TestRepositoryFactory.CreateProjectRepository()),
            principalAccess,
            accessor,
            new DefaultIdGenerator(),
            dispatch ?? new RecordingDispatchService());
    }

    private sealed class GuidParsingDisplayIdService : IDisplayIdService
    {
        public Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default)
        {
            _ = kind;
            return Task.FromResult(Guid.TryParse(idOrUuid, out var id) ? id : (Guid?)null);
        }

        public Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(
            string kind,
            IEnumerable<Guid> resourceIds,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubPrincipalDataAccess : IPrincipalDataAccess
    {
        private readonly Dictionary<Guid, PrincipalInfo> _principals = [];

        public void Add(Guid principalId, Guid tenantId, PrincipalType type, bool isActive) =>
            _principals[principalId] = new PrincipalInfo(principalId, tenantId, type, isActive, null, null);

        public Task<PrincipalInfo?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(_principals.TryGetValue(principalId, out var info) ? info : null);

        public Task<TenantInfo?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<TenantInfo?>(new TenantInfo(tenantId, "default", TenantLifecycle.Active));

        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GroupSnapshot>>([]);
    }

    private static PrincipalResourceGrantRow Grant(Guid principalId, string kind, Guid resourceId) =>
        new()
        {
            PrincipalId = principalId,
            ResourceKind = kind,
            ResourceId = resourceId,
            CreatedAt = DateTime.UtcNow
        };

    private sealed class MutableGrantStore : IPrincipalResourceGrantStore
    {
        public List<PrincipalResourceGrantRow> Rows { get; } = [];

        /// <inheritdoc />
        public Task<IReadOnlyList<PrincipalResourceGrantRow>> ListAsync(
            Guid principalId,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            IReadOnlyList<PrincipalResourceGrantRow> matched = Rows
                .Where(row => row.PrincipalId == principalId)
                .ToList();
            return Task.FromResult(matched);
        }

        /// <inheritdoc />
        public Task ReplaceAsync(
            Guid principalId,
            IReadOnlyCollection<Guid> projectIds,
            IReadOnlyCollection<Guid> definitionIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingDispatchService : IExecutionScheduleDispatchService
    {
        public int RunCount { get; private set; }
        public string? LastIdempotencyKey { get; private set; }
        public Guid StartedExecutionId { get; } = Guid.NewGuid();

        public Task<int> DispatchDueAsync(DateTime utcNow, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<ExecutionResponse> RunManuallyAsync(
            Guid scheduleId,
            string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            _ = scheduleId;
            _ = cancellationToken;
            RunCount++;
            LastIdempotencyKey = idempotencyKey;
            return Task.FromResult(new ExecutionResponse
            {
                DisplayId = "exec-manual",
                ResourceId = StartedExecutionId,
                Status = "Running"
            });
        }
    }
}
