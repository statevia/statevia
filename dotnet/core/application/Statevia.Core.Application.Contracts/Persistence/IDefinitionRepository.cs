namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>definitions / definition_versions の素の永続化ポート。project 認可は呼び出し元の責任である。</summary>
public interface IDefinitionRepository
{
    /// <summary>Active catalog の最新版を取得する（GET / Graph 用）。認可しない。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>最新版を含む定義。無いか論理削除済みなら <see langword="null"/>。</returns>
    Task<DefinitionDetail?> GetLatestForApiAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>Active catalog 行を変更用に取得する（publish / soft delete 前提）。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>追跡中の定義の catalog。無いか論理削除済みなら <see langword="null"/>。</returns>
    Task<DefinitionRow?> GetLatestForMutationAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>版行を取得する（親 definition の deleted_at は bypass。実行鎖用）。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="version">定義の版番号（1 始まり）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>版。無ければ <see langword="null"/>。</returns>
    Task<DefinitionVersionRow?> GetVersionForExecutionAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct);

    /// <summary>版 ID で版行を取得する（親 definition の deleted_at は bypass）。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="definitionVersionId">定義の版 ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>版。無ければ <see langword="null"/>。</returns>
    Task<DefinitionVersionRow?> GetVersionForExecutionByIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionVersionId,
        CancellationToken ct);

    /// <summary>親 catalog が active な版行を取得する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="version">定義の版番号（1 始まり）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>版。無いか親の catalog が論理削除済みなら <see langword="null"/>。</returns>
    Task<DefinitionVersionRow?> GetVersionForApiAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        int version,
        CancellationToken ct);

    /// <summary>削除済み catalog 行を取得する（restore 用）。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>論理削除済みの catalog。無ければ <see langword="null"/>。</returns>
    Task<DefinitionRow?> GetDeletedCatalogEntryAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>active catalog の project_id を返す。論理削除済みまたは無ければ null。認可しない。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>Project ID。無いか論理削除済みなら <see langword="null"/>。</returns>
    Task<Guid?> ResolveProjectIdAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>catalog（論理削除含む）の project_id を返す。無ければ null。実行読取認可用。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>Project ID。無ければ <see langword="null"/>。</returns>
    Task<Guid?> ResolveProjectIdIncludingDeletedAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>定義と最初の版をユニットオブワークに追加する。SaveChanges は呼び出し側。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="definition">追加する定義。</param>
    /// <param name="version">追加する最初の版。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>追加が終わると完了するタスク。</returns>
    Task AddWithInitialVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionRow definition,
        DefinitionVersionRow version,
        CancellationToken ct);

    /// <summary>Active な定義に次の版を追加し、<c>latest_version</c> と名前を更新する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="command">publish する内容。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>追加した版を含む定義。Active な定義が無ければ <see langword="null"/>。</returns>
    Task<DefinitionDetail?> PublishVersionAsync(
        ICoreUnitOfWork uow,
        DefinitionVersionPublishCommand command,
        CancellationToken ct);

    /// <summary>呼び出し元 Principal が Reader 以上の Project に属する定義だけをページングする。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="principalId">Principal ID。</param>
    /// <param name="query">ページングと絞り込みの条件。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>絞り込み後の総件数と、ページ内の定義と表示用 ID。</returns>
    Task<(int TotalCount, List<(DefinitionDetail Detail, string? DisplayId)> Items)> ListWithDisplayIdsPageAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        DefinitionListPageQuery query,
        CancellationToken ct);

    /// <summary>catalog を論理削除する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="deletedAt">論理削除日時（UTC）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>論理削除の結果（削除した、無い、など）。</returns>
    Task<DefinitionSoftDeleteOutcome> SoftDeleteAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        DateTime deletedAt,
        CancellationToken ct);

    /// <summary>
    /// 削除済み catalog を復元し、同一 UoW 内で応答用の最新版詳細を返す。
    /// </summary>
    /// <remarks>
    /// SaveChanges 前でも追跡エンティティから詳細を組み立てる。
    /// AsNoTracking + activeOnly の再取得だと DB 上はまだ削除済みのため null になる。
    /// </remarks>
    /// <returns>復元できた場合は詳細。対象が無い場合は null。</returns>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task<DefinitionDetail?> RestoreAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct);

    /// <summary>同一 project 内に active な slug 競合があるか。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="slug">Project 内 slug。</param>
    /// <param name="excludingDefinitionId">比較から外す定義 ID（自分自身）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>自分以外の Active な定義が同じ slug を使っていれば <see langword="true"/>。</returns>
    Task<bool> ExistsActiveSlugInProjectAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        string slug,
        Guid excludingDefinitionId,
        CancellationToken ct);
}
