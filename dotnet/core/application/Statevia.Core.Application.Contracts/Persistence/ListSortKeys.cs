namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>一覧の並び順（<c>sortOrder</c>）の許可する値。</summary>
/// <remarks>大文字小文字を区別する。許可する値の外は API が 422 にする。省略時は <see cref="Desc"/>。</remarks>
public static class ListSortOrders
{
    /// <summary>昇順。</summary>
    public const string Asc = "asc";

    /// <summary>降順（既定）。</summary>
    public const string Desc = "desc";

    /// <summary>許可する値（エラーの説明と検証に使う）。</summary>
    public static IReadOnlyCollection<string> All { get; } = [Asc, Desc];

    /// <summary>並び順が昇順かを返す。</summary>
    /// <param name="sortOrder">検証済みの値。<see langword="null"/> は既定の降順。</param>
    /// <returns>昇順なら <see langword="true"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException">許可する値の外のとき（検証を通っていない値）。</exception>
    public static bool IsAscending(string? sortOrder) =>
        sortOrder switch
        {
            Asc => true,
            null or Desc => false,
            _ => throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, "Unsupported sort order.")
        };
}

/// <summary>実行の一覧（<c>GET /v1/executions</c>）の <c>sortBy</c> の許可する値。</summary>
/// <remarks>大文字小文字を区別する。省略時は <see cref="UpdatedAt"/>。</remarks>
public static class ExecutionListSortKeys
{
    /// <summary>最終更新日時（既定）。</summary>
    public const string UpdatedAt = "updatedAt";

    /// <summary>表示 ID。</summary>
    public const string DisplayId = "displayId";

    /// <summary>開始日時。</summary>
    public const string StartedAt = "startedAt";

    /// <summary>許可する値（エラーの説明と検証に使う）。</summary>
    public static IReadOnlyCollection<string> All { get; } = [UpdatedAt, DisplayId, StartedAt];
}

/// <summary>定義の一覧（<c>GET /v1/definitions</c>）の <c>sortBy</c> の許可する値。</summary>
/// <remarks>大文字小文字を区別する。省略時は <see cref="CreatedAt"/>。</remarks>
public static class DefinitionListSortKeys
{
    /// <summary>作成日時（既定）。</summary>
    public const string CreatedAt = "createdAt";

    /// <summary>定義名。</summary>
    public const string Name = "name";

    /// <summary>更新日時。</summary>
    public const string UpdatedAt = "updatedAt";

    /// <summary>許可する値（エラーの説明と検証に使う）。</summary>
    public static IReadOnlyCollection<string> All { get; } = [CreatedAt, Name, UpdatedAt];
}
