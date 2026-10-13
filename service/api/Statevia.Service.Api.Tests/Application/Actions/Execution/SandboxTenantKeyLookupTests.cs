using Microsoft.Extensions.DependencyInjection;
using Statevia.Service.Api.Application.Actions.Execution;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="SandboxTenantKeyLookup"/> がスコープ内のテナント行から tenant_key を読むこと。</summary>
public sealed class SandboxTenantKeyLookupTests
{
    /// <summary>行があるときはその tenant_key を返す。</summary>
    [Fact]
    public async Task FindTenantKeyAsync_WhenTenantExists_ReturnsKey()
    {
        // Arrange
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var principals = new FixedTenantDataAccess(new TenantInfo(tenantId, "acme", TenantLifecycle.Active));
        var sut = new SandboxTenantKeyLookup(new SingleScopeFactory(principals));

        // Act
        var tenantKey = await sut.FindTenantKeyAsync(tenantId, CancellationToken.None);

        // Assert
        Assert.Equal("acme", tenantKey);
    }

    /// <summary>行が無いときは null を返す。</summary>
    [Fact]
    public async Task FindTenantKeyAsync_WhenTenantMissing_ReturnsNull()
    {
        // Arrange
        var principals = new FixedTenantDataAccess(tenant: null);
        var sut = new SandboxTenantKeyLookup(new SingleScopeFactory(principals));

        // Act
        var tenantKey = await sut.FindTenantKeyAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(tenantKey);
    }

    private sealed class SingleScopeFactory(IPrincipalDataAccess principals) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new Scope(principals);
    }

    private sealed class Scope(IPrincipalDataAccess principals) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new Provider(principals);

        public void Dispose()
        {
        }
    }

    private sealed class Provider(IPrincipalDataAccess principals) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IPrincipalDataAccess) ? principals : null;
    }

    private sealed class FixedTenantDataAccess(TenantInfo? tenant) : IPrincipalDataAccess
    {
        public Task<PrincipalInfo?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantInfo?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            _ = tenantId;
            _ = cancellationToken;
            return Task.FromResult(tenant);
        }

        public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
            Guid principalId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
