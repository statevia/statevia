namespace Statevia.Core.Application.Contracts.Services;

/// <summary>表示用 ID（10 桁・62 文字種）の UUID 解決。</summary>
public interface IDisplayIdService
{
    /// <summary>表示用 ID または UUID 文字列から UUID を解決する。見つからなければ null。</summary>
    /// <param name="kind">リソース種別（<c>definition</c> / <c>execution</c>）。</param>
    /// <param name="idOrUuid">表示用 ID または UUID の文字列。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>UUID を返すタスク。見つからなければ <see langword="null"/>。</returns>
    Task<Guid?> ResolveAsync(string kind, string idOrUuid, CancellationToken ct = default);

    /// <summary>表示用 ID を返す。idOrUuid が display_id 形式（UUID でない）なら検索せずそのまま返す。</summary>
    /// <param name="kind">リソース種別（<c>definition</c> / <c>execution</c>）。</param>
    /// <param name="idOrUuid">表示用 ID または UUID の文字列。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>表示用 ID を返すタスク。見つからなければ <see langword="null"/>。</returns>
    Task<string?> GetDisplayIdAsync(string kind, string idOrUuid, CancellationToken ct = default);

    /// <summary>複数 resource_id の表示用 ID を 1 クエリで取得する。一覧 API の N+1 防止用。</summary>
    /// <param name="kind">リソース種別（<c>definition</c> / <c>execution</c>）。</param>
    /// <param name="resourceIds">リソースの UUID の一覧。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>UUID → 表示用 ID を返すタスク。見つからない UUID は含めない。</returns>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayIdsAsync(string kind, IEnumerable<Guid> resourceIds, CancellationToken ct = default);
}
