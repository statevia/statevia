namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary><c>command_dedup</c>（冪等キーによる Command の重複排除）の永続化。</summary>
public interface ICommandDedupRepository
{
    /// <summary>有効期限内の冪等キーの記録を合成キーで探す。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="dedupKey">冪等キーの合成キー（<c>command_dedup.dedup_key</c>）。</param>
    /// <param name="utcNow">判定に使う現在時刻（UTC）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>有効な記録。無ければ <see langword="null"/>。</returns>
    Task<CommandDedupRow?> FindValidAsync(ICoreUnitOfWork uow, string dedupKey, DateTime utcNow, CancellationToken ct);

    /// <summary>同じテナント・エンドポイント・冪等キーで、本文ハッシュが違う（または無い）有効な記録を探す。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="tenantKey">テナントキー。</param>
    /// <param name="endpoint">HTTP メソッドとパス。</param>
    /// <param name="idempotencyKey">冪等キー（<c>X-Idempotency-Key</c> の値）。</param>
    /// <param name="requestHash">リクエスト本文のハッシュ。</param>
    /// <param name="utcNow">判定に使う現在時刻（UTC）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>本文ハッシュが食い違う有効な記録。無ければ <see langword="null"/>。</returns>
    Task<CommandDedupRow?> FindValidConflictingRequestHashAsync(
        ICoreUnitOfWork uow,
        string tenantKey,
        string endpoint,
        string idempotencyKey,
        string requestHash,
        DateTime utcNow,
        CancellationToken ct);

    /// <summary>冪等キーの記録をユニットオブワークに追加する。SaveChanges は呼び出し側。</summary>
    /// <param name="uow">参加中のユニットオブワーク。</param>
    /// <param name="row">保存する冪等キーの記録。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>追加が終わると完了するタスク。</returns>
    Task SaveAsync(ICoreUnitOfWork uow, CommandDedupRow row, CancellationToken ct);
}
