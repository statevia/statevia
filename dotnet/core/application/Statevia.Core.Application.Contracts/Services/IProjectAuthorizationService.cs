namespace Statevia.Core.Application.Contracts.Services;

/// <summary>project_accesses の Principal メンバーシップを認可の truth とする。</summary>
public interface IProjectAuthorizationService
{
    /// <summary>Reader 以上の権限を要求する。不足時は <see cref="NotFoundException"/>。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>認可を確かめ終わると完了するタスク。拒否するときは例外で失敗する。</returns>
    Task EnsureCanReadAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Executor 以上の権限を要求する。Reader のみの場合は <see cref="Security.ForbiddenException"/>。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>認可を確かめ終わると完了するタスク。拒否するときは例外で失敗する。</returns>
    Task EnsureCanExecuteAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Publisher 以上の権限を要求する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>認可を確かめ終わると完了するタスク。拒否するときは例外で失敗する。</returns>
    Task EnsureCanPublishAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>
    /// メンバー管理を要求する。当該 Project の <c>project.admin</c>、または同一テナントのテナント管理者。
    /// 未所属は <see cref="NotFoundException"/>。所属で admin 未満は <see cref="Security.ForbiddenException"/>。
    /// </summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>認可を確かめ終わると完了するタスク。拒否するときは例外で失敗する。</returns>
    Task EnsureCanManageMembersAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>
    /// Project 設定変更（表示名・説明・slug）を要求する。当該 Project の <c>project.admin</c> のみ（tenant.admin は注入しない）。
    /// </summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>認可を確かめ終わると完了するタスク。拒否するときは例外で失敗する。</returns>
    Task EnsureCanAdminAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);
}
