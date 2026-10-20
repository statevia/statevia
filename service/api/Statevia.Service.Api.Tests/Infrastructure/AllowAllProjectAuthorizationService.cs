namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>ユニットテスト用 — project 認可を常に許可する。</summary>
internal sealed class AllowAllProjectAuthorizationService : IProjectAuthorizationService
{
    /// <inheritdoc />
    public Task EnsureCanReadAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        Allow(uow, tenantId, projectId, ct);

    /// <inheritdoc />
    public Task EnsureCanExecuteAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        EnsureCanReadAsync(uow, tenantId, projectId, ct);

    /// <inheritdoc />
    public Task EnsureCanPublishAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        EnsureCanExecuteAsync(uow, tenantId, projectId, ct);

    /// <inheritdoc />
    public Task EnsureCanManageMembersAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct) =>
        EnsureCanPublishAsync(uow, tenantId, projectId, ct);

    private static Task Allow(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantId;
        _ = projectId;
        _ = ct;
        return Task.CompletedTask;
    }
}
