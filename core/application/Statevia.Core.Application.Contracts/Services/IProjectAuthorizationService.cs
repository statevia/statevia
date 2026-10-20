namespace Statevia.Core.Application.Contracts.Services;

/// <summary>project_accesses の Principal メンバーシップを認可の truth とする。</summary>
public interface IProjectAuthorizationService
{
    /// <summary>Reader 以上の権限を要求する。不足時は <see cref="NotFoundException"/>。</summary>
    Task EnsureCanReadAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Executor 以上の権限を要求する。Reader のみの場合は <see cref="Security.ForbiddenException"/>。</summary>
    Task EnsureCanExecuteAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Publisher 以上の権限を要求する。</summary>
    Task EnsureCanPublishAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>
    /// メンバー管理を要求する。当該 Project の <c>project.admin</c>、または同一テナントのテナント管理者。
    /// 未所属は <see cref="NotFoundException"/>。所属で admin 未満は <see cref="Security.ForbiddenException"/>。
    /// </summary>
    Task EnsureCanManageMembersAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);
}
