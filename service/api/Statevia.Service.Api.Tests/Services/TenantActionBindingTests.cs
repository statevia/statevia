using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Service.Api.Contracts.Admin;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>テナント Action バインディングの保存、非返却、解決。</summary>
public sealed class TenantActionBindingTests
{
    private const string SecretValue = "binding-secret-value-9f3a";
    private const string ActionId = "acme.order.query";

    /// <summary>シークレットの作成応答と一覧に値が無く、変数の値はある。</summary>
    [Fact]
    public async Task CreateAndList_OmitsSecretValue()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "bind-admin@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);

        // Act
        var secret = await service.CreateAsync(adminId, Create("order-db", "secret", SecretValue), CancellationToken.None);
        var variable = await service.CreateAsync(adminId, Create("order-page-size", "variable", "25"), CancellationToken.None);
        var listed = await service.ListAsync(adminId, CancellationToken.None);
        var json = JsonSerializer.Serialize(listed);

        // Assert
        Assert.True(secret.ValueConfigured);
        Assert.Null(secret.Value);
        Assert.Equal("25", variable.Value);
        Assert.False(variable.ValueConfigured);
        Assert.Equal(2, listed.Count);
        Assert.DoesNotContain(SecretValue, json, StringComparison.Ordinal);
    }

    /// <summary>保護用途は行 ID に結び、他テナントの暗号文は復元できない。</summary>
    [Fact]
    public async Task Create_ProtectsSecretForOwningTenantOnly()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "bind-protect@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var protector = CreateDevelopmentProtector();
        var service = CreateService(database, tenantContext, adminId, protector);
        await service.CreateAsync(adminId, Create("order-db", "secret", SecretValue), CancellationToken.None);
        await using var db = await database.Factory.CreateDbContextAsync();
        var row = await db.TenantActionBindings.SingleAsync();

        // Act
        var restored = protector.Unprotect(row.TenantId, TenantActionBindingPurpose.For(row.BindingId), row.ValueProtected!);
        var otherTenant = Assert.Throws<InvalidOperationException>(() =>
            protector.Unprotect(Guid.NewGuid(), TenantActionBindingPurpose.For(row.BindingId), row.ValueProtected!));

        // Assert
        Assert.Equal(SecretValue, restored);
        Assert.Null(row.ValuePlaintext);
        Assert.DoesNotContain(SecretValue, otherTenant.Message, StringComparison.Ordinal);
    }

    /// <summary>シークレット更新で値を省略すると暗号文を維持し、送ると置換する。</summary>
    [Fact]
    public async Task Update_SecretValueOmitted_KeepsCiphertext()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "bind-update@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var protector = CreateDevelopmentProtector();
        var service = CreateService(database, tenantContext, adminId, protector);
        await service.CreateAsync(adminId, Create("order-db", "secret", SecretValue), CancellationToken.None);
        await using var beforeDb = await database.Factory.CreateDbContextAsync();
        var before = await beforeDb.TenantActionBindings.AsNoTracking().SingleAsync();

        // Act
        var kept = await service.UpdateAsync(
            adminId,
            "order-db",
            new UpdateTenantActionBindingRequest
            {
                Kind = "secret",
                AllowedActionIds = ["acme.order.other"],
            },
            CancellationToken.None);
        await using var keptDb = await database.Factory.CreateDbContextAsync();
        var keptRow = await keptDb.TenantActionBindings.AsNoTracking().Include(row => row.AllowedActions).SingleAsync();
        const string nextValue = "next-binding-secret";
        await service.UpdateAsync(
            adminId,
            "order-db",
            new UpdateTenantActionBindingRequest
            {
                Kind = "secret",
                Value = nextValue,
                AllowedActionIds = [ActionId],
            },
            CancellationToken.None);
        await using var afterDb = await database.Factory.CreateDbContextAsync();
        var after = await afterDb.TenantActionBindings.AsNoTracking().SingleAsync();
        var restored = protector.Unprotect(after.TenantId, TenantActionBindingPurpose.For(after.BindingId), after.ValueProtected!);

        // Assert
        Assert.Null(kept.Value);
        Assert.Equal(before.ValueProtected, keptRow.ValueProtected);
        Assert.Contains("acme.order.other", keptRow.AllowedActions.Select(action => action.ActionId));
        Assert.NotEqual(before.ValueProtected, after.ValueProtected);
        Assert.Equal(nextValue, restored);
        Assert.DoesNotContain(nextValue, JsonSerializer.Serialize(kept), StringComparison.Ordinal);
    }

    /// <summary>非管理者は読めない。隣接テナントの同名は返らない。</summary>
    [Fact]
    public async Task List_HidesOtherTenantAndNonAdmin()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "bind-boundary@example.com", "password", isTenantAdmin: true);
        var memberId = await SecurityTestSeed.SeedUserAsync(database, "bind-member@example.com", "password", isTenantAdmin: false);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);
        await service.CreateAsync(adminId, Create("order-db", "variable", "local"), CancellationToken.None);
        await using (var db = await database.Factory.CreateDbContextAsync())
        {
            var foreign = new TenantActionBindingRow
            {
                BindingId = Guid.NewGuid(),
                TenantId = TestTenantIds.T1TenantId,
                Name = "order-db",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            foreign.SetVariable("foreign-value");
            db.TenantActionBindings.Add(foreign);
            await db.SaveChangesAsync();
        }

        tenantContext.Set(TestTenantIds.DefaultContext with { PrincipalId = adminId });

        // Act
        var listed = await service.ListAsync(adminId, CancellationToken.None);
        var denied = await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAsync(memberId, CancellationToken.None));

        // Assert
        Assert.Single(listed);
        Assert.Equal("local", listed[0].Value);
        Assert.DoesNotContain("foreign-value", JsonSerializer.Serialize(listed), StringComparison.Ordinal);
        Assert.Equal("FORBIDDEN", denied.Code);
    }

    /// <summary>65 件目、9 個の許可、4097 バイト、種別変更は拒否し、メッセージに秘密を含めない。</summary>
    [Fact]
    public async Task Limits_RejectOutsideContractWithoutEchoingSecret()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "bind-limits@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);
        for (var index = 0; index < TenantActionBindingService.MaxBindingsPerTenant; index++)
        {
            await service.CreateAsync(adminId, Create($"name{index}", "variable", "v"), CancellationToken.None);
        }

        var oversized = new string('x', TenantActionBindingService.MaxValueUtf8Bytes + 1);
        var tooManyActions = Enumerable.Range(0, 9).Select(index => $"acme.order.a{index}").ToArray();

        // Act
        var count = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(adminId, Create("extra", "variable", "v"), CancellationToken.None));
        var actions = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(adminId, Create("extra", "secret", SecretValue, tooManyActions), CancellationToken.None));
        var bytes = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(adminId, Create("extra", "secret", oversized), CancellationToken.None));
        await service.DeleteAsync(adminId, "name0", CancellationToken.None);
        await service.CreateAsync(adminId, Create("order-db", "secret", SecretValue), CancellationToken.None);
        var kind = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(
                adminId,
                "order-db",
                new UpdateTenantActionBindingRequest
                {
                    Kind = "variable",
                    Value = "plain",
                    AllowedActionIds = [ActionId],
                },
                CancellationToken.None));

        // Assert
        Assert.Contains("64", count.Message, StringComparison.Ordinal);
        Assert.Contains("8", actions.Message, StringComparison.Ordinal);
        Assert.Contains("4096", bytes.Message, StringComparison.Ordinal);
        Assert.Contains("kind", kind.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretValue, actions.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(oversized, bytes.Message, StringComparison.Ordinal);
    }

    /// <summary>許可された action だけがスロットを受け、隣接テナントの同名は使わない。</summary>
    [Fact]
    public async Task Resolve_UsesExecutionTenantAndAllowedAction()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "bind-resolve@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var protector = CreateDevelopmentProtector();
        var service = CreateService(database, tenantContext, adminId, protector);
        await service.CreateAsync(adminId, Create("order-db", "secret", SecretValue), CancellationToken.None);
        await service.CreateAsync(adminId, Create("order-page-size", "variable", "25"), CancellationToken.None);
        await using (var db = await database.Factory.CreateDbContextAsync())
        {
            var foreign = new TenantActionBindingRow
            {
                BindingId = Guid.NewGuid(),
                TenantId = TestTenantIds.T1TenantId,
                Name = "order-db",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            foreign.SetSecret(protector.Protect(TestTenantIds.T1TenantId, TenantActionBindingPurpose.For(foreign.BindingId), "foreign-secret"));
            foreign.AllowedActions.Add(new TenantActionBindingActionRow { BindingId = foreign.BindingId, ActionId = ActionId });
            db.TenantActionBindings.Add(foreign);
            await db.SaveChangesAsync();
        }

        var resolver = new ActionBindingResolver(database.Factory, protector);
        var references = new ActionBindingReferences(
            new Dictionary<string, string> { ["connection"] = "order-db" },
            new Dictionary<string, string> { ["pageSize"] = "order-page-size" });

        // Act
        var allowed = await resolver.ResolveAsync(Request(TestTenantIds.DefaultTenantId, ActionId, references), CancellationToken.None);
        var denied = await resolver.ResolveAsync(Request(TestTenantIds.DefaultTenantId, "acme.order.other", references), CancellationToken.None);
        var foreignTenant = await resolver.ResolveAsync(Request(TestTenantIds.T1TenantId, ActionId, new ActionBindingReferences(
            new Dictionary<string, string> { ["connection"] = "order-db" },
            new Dictionary<string, string>())), CancellationToken.None);
        using (ActionBindingScope.Begin(allowed.Request.Bindings))
        {
            Assert.Equal(SecretValue, ActionBindingScope.Current["connection"]);
        }

        // Assert
        Assert.Null(allowed.Failure);
        Assert.Equal("25", allowed.Request.Bindings!["pageSize"]);
        Assert.Equal("ActionBindingUnavailable", denied.Failure!.ErrorCode);
        Assert.Contains("not allowed", denied.Failure.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretValue, denied.Failure.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(foreignTenant.Failure);
        Assert.Equal("foreign-secret", foreignTenant.Request.Bindings!["connection"]);
        Assert.DoesNotContain(SecretValue, foreignTenant.Request.Bindings["connection"], StringComparison.Ordinal);
        Assert.Empty(ActionBindingScope.Current);
        Environment.SetEnvironmentVariable("STATEVIA_BINDING_CONNECTION", SecretValue);
        var fromEnvironment = await resolver.ResolveAsync(
            Request(TestTenantIds.DefaultTenantId, ActionId, new ActionBindingReferences(
                new Dictionary<string, string> { ["connection"] = "missing-name" },
                new Dictionary<string, string>())),
            CancellationToken.None);
        Environment.SetEnvironmentVariable("STATEVIA_BINDING_CONNECTION", null);
        Assert.Equal("ActionBindingUnavailable", fromEnvironment.Failure!.ErrorCode);
        Assert.DoesNotContain(SecretValue, fromEnvironment.Failure.ErrorMessage, StringComparison.Ordinal);
    }

    private static ActionExecutionRequest Request(Guid tenantId, string actionId, ActionBindingReferences references) =>
        new()
        {
            ExecutionId = "exec",
            StateName = "Query",
            ActionId = actionId,
            TenantId = tenantId.ToString("D"),
            BindingReferences = references,
        };

    private static CreateTenantActionBindingRequest Create(
        string name,
        string kind,
        string value,
        IReadOnlyList<string>? actionIds = null) =>
        new()
        {
            Name = name,
            Kind = kind,
            Value = value,
            AllowedActionIds = actionIds ?? [ActionId],
        };

    private static TenantActionBindingService CreateService(
        SqliteTestDatabase database,
        SettableTenantContextAccessor tenantContext,
        Guid callerPrincipalId,
        TenantSecretProtector? protector = null)
    {
        tenantContext.Set(TestTenantIds.DefaultContext with { PrincipalId = callerPrincipalId });
        return new TenantActionBindingService(
            database.Factory,
            tenantContext,
            new TenantAdminAuthorization(new PlatformDataAccess(database.Factory, new DefaultIdGenerator())),
            protector ?? CreateDevelopmentProtector(),
            new DefaultIdGenerator(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantActionBindingService>.Instance);
    }

    private static TenantSecretProtector CreateDevelopmentProtector() =>
        new(
            Options.Create(new SecretProtectionOptions()),
            new DevelopmentHostEnvironment(),
            new ConfigurationBuilder().Build());

    /// <summary>バインディングテスト用の Development ホスト。</summary>
    private sealed class DevelopmentHostEnvironment : IHostEnvironment
    {
        /// <inheritdoc />
        public string EnvironmentName { get; set; } = Environments.Development;

        /// <inheritdoc />
        public string ApplicationName { get; set; } = "Statevia.Service.Api.Tests";

        /// <inheritdoc />
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        /// <inheritdoc />
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
