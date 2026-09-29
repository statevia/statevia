namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>Principal の project / definition 許可の正本。</summary>
/// <remarks>
/// 読取と置換は呼び出しスコープのテナントに閉じる。他テナントの行は返さず、置換もしない。
/// </remarks>
public interface IPrincipalResourceGrantStore
{
    /// <summary>当該 Principal の許可を、解決済みテナントの範囲で返す。</summary>
    /// <param name="principalId">対象 Principal。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>テナント未解決のときは空。種別と resource ID の順。</returns>
    Task<IReadOnlyList<PrincipalResourceGrantRow>> ListAsync(
        Guid principalId,
        CancellationToken cancellationToken);

    /// <summary>project と definition の許可を一括で置き換える。</summary>
    /// <param name="principalId">対象 Principal。</param>
    /// <param name="projectIds">残す project ID。空なら project 許可を消す。重複は 1 件にする。</param>
    /// <param name="definitionIds">残す定義 ID。空なら definition 許可を消す。重複は 1 件にする。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <exception cref="ArgumentException">ID が空 GUID のとき。</exception>
    /// <exception cref="InvalidOperationException">テナント文脈が未解決のとき。</exception>
    Task ReplaceAsync(
        Guid principalId,
        IReadOnlyCollection<Guid> projectIds,
        IReadOnlyCollection<Guid> definitionIds,
        CancellationToken cancellationToken);
}
