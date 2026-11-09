using Statevia.Core.Application.Contracts.Security;

namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>projects / project_accesses 永続化。</summary>
public interface IProjectRepository
{
    /// <summary>
    /// テナントの <c>slug=default</c> を返す。無いときだけ作り、そのときだけ作成者を <c>admin</c> にする。
    /// 既存行には呼び出し元を足さない。
    /// </summary>
    /// <param name="uow">作業単位。</param>
    /// <param name="tenantId">所属テナント。</param>
    /// <param name="tenantKey">表示名に使うテナントキー。</param>
    /// <param name="creatorPrincipalId">新規作成時の初期 admin。同一テナントの Principal。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="ApiValidationException">作成者 Principal が同一テナントに無いとき。</exception>
    /// <returns><c>slug=default</c> の Project。</returns>
    Task<ProjectRow> EnsureDefaultProjectAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        string tenantKey,
        Guid creatorPrincipalId,
        CancellationToken ct);

    /// <summary>当該 Principal のメンバー行だけを実効ロールにする。所属テナントが違う、またはメンバー行が無いときは null。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="principalId">Principal ID。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>実効ロール。テナントが違うかメンバーでなければ <see langword="null"/>。</returns>
    Task<ProjectAccessRole?> ResolveEffectiveRoleAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Project が指定テナントに所属するか。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>所属していれば <see langword="true"/>。</returns>
    Task<bool> ExistsInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>同一テナントの Principal に Project ロールを付与する。</summary>
    /// <exception cref="ApiValidationException">Principal と Project のテナントが一致しないとき。</exception>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="principalId">Principal ID。</param>
    /// <param name="role">Project のロール。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>付与が終わると完了するタスク。</returns>
    Task GrantAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        ProjectAccessRole role,
        CancellationToken ct);

    /// <summary>呼び出し元 Principal がメンバーの Project と実効ロールを一覧する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="principalId">Principal ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>Project と実効ロールの一覧。</returns>
    Task<IReadOnlyList<(ProjectRow Project, ProjectAccessRole Role)>> ListMembershipAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid principalId,
        CancellationToken ct);

    /// <summary>テナント内の Project を取得する（追跡しない）。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>Project。無ければ <see langword="null"/>。</returns>
    Task<ProjectRow?> GetInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>テナント内の Project を更新用に追跡取得する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>追跡中の Project。無ければ <see langword="null"/>。</returns>
    Task<ProjectRow?> GetInTenantForUpdateAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Project 行を追加する。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="project">追加する Project。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>追加が終わると完了するタスク。</returns>
    Task AddAsync(ICoreUnitOfWork uow, ProjectRow project, CancellationToken ct);

    /// <summary>テナント内で slug が既に使われているか。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="slug">テナント内 slug。</param>
    /// <param name="excludingProjectId">比較から外す Project ID（自分自身の更新時）。null なら外さない。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>使われていれば <see langword="true"/>。</returns>
    Task<bool> ExistsSlugInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        string slug,
        Guid? excludingProjectId,
        CancellationToken ct);

    /// <summary>テナント内の Project を slug 順で返す（メンバーシップでは絞らない）。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>Project の一覧（slug 順）。</returns>
    Task<IReadOnlyList<ProjectRow>> ListInTenantAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        CancellationToken ct);

    /// <summary>Project のメンバーを表示名付きで作成日時順に返す。Principal 行が無いメンバーは含めない。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>メンバーの一覧（作成日時順）。</returns>
    Task<IReadOnlyList<ProjectMemberDetail>> ListMemberDetailsAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>
    /// 付与候補。削除されていない有効なユーザー Principal と ServiceAccount Principal。System は含めない。
    /// </summary>
    /// <remarks>
    /// ServiceAccount は API キーの有無で絞らない。資格のない ServiceAccount も、定期実行の run-as として
    /// 定義の Project に所属させる必要があるため。
    /// </remarks>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantId">テナントの内部 ID（<c>tenants.tenant_id</c>）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>付与候補の Principal の一覧。</returns>
    Task<IReadOnlyList<ProjectPrincipalLabel>> ListGrantCandidatesAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        CancellationToken ct);

    /// <summary>Project のメンバー行を作成日時順で返す。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>メンバーの一覧（作成日時順）。</returns>
    Task<IReadOnlyList<ProjectAccessRow>> ListMembersAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        CancellationToken ct);

    /// <summary>メンバー行を更新用に追跡取得する。無いときは null。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="principalId">Principal ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>追跡中のメンバー。無ければ <see langword="null"/>。</returns>
    Task<ProjectAccessRow?> GetAccessForUpdateAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        CancellationToken ct);

    /// <summary>指定ロールのメンバー数。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="role">Project のロール。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>メンバー数。</returns>
    Task<int> CountByRoleAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        ProjectAccessRole role,
        CancellationToken ct);

    /// <summary>メンバー行が無ければ付与し、あればロールを上書きする。</summary>
    /// <exception cref="ApiValidationException">Principal と Project のテナントが一致しないとき。</exception>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="principalId">Principal ID。</param>
    /// <param name="role">Project のロール。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>書き込みが終わると完了するタスク。</returns>
    Task UpsertAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        ProjectAccessRole role,
        CancellationToken ct);

    /// <summary>メンバー行を削除する。行が無ければ false。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="projectId">Project ID。</param>
    /// <param name="principalId">Principal ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>削除したら <see langword="true"/>。メンバーでなければ <see langword="false"/>。</returns>
    Task<bool> RemoveAccessAsync(
        ICoreUnitOfWork uow,
        Guid projectId,
        Guid principalId,
        CancellationToken ct);

    /// <summary>
    /// 既存 <c>default</c> に、削除されていないユーザー Principal と API キー Principal を <c>admin</c> で足す。
    /// 既にあるメンバー行は上書きしない。再実行で増えない。System と API キーでないサービスアカウントは対象外。
    /// </summary>
    /// <param name="uow">作業単位。</param>
    /// <param name="ct">キャンセル。</param>
    /// <returns>挿入した行数。</returns>
    /// <remarks>本番の一度きり適用はマイグレーション <c>BackfillDefaultProjectAdmins</c> の SQL。述語はそちらと揃える。</remarks>
    Task<int> BackfillDefaultProjectAdminsAsync(ICoreUnitOfWork uow, CancellationToken ct);
}
