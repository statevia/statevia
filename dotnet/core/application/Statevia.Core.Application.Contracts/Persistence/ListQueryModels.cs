namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>ページング（offset / limit）。</summary>
public sealed record PageQuery(
    int Offset,
    int Limit);

/// <summary>ソート指定。</summary>
public sealed record SortQuery(
    string? SortBy,
    string? SortOrder);

/// <summary>定義一覧のページクエリ。</summary>
/// <param name="Page">ページ。</param>
/// <param name="Sort">ソート。</param>
/// <param name="NameContains">定義名の部分一致。未指定は絞らない。</param>
/// <param name="IncludeDeleted">削除済みを含める。</param>
/// <param name="ProjectId">指定時はその Project の定義だけ。未指定は所属 Project の和。</param>
public sealed record DefinitionListPageQuery(
    PageQuery Page,
    SortQuery Sort,
    string? NameContains,
    bool IncludeDeleted = false,
    Guid? ProjectId = null);

/// <summary>実行一覧のページクエリ。</summary>
/// <param name="Page">ページ。</param>
/// <param name="Sort">ソート。</param>
/// <param name="StatusFilter">ステータス（投影の値）。未指定は絞らない。</param>
/// <param name="DefinitionIdFilter">定義 UUID。未指定は絞らない。</param>
/// <param name="NameContains">表示 ID の部分一致、または execution UUID の完全一致。</param>
/// <param name="ProjectId">指定時はその Project の定義に紐づく実行だけ。未所属の Project は 0 件。未指定は所属の和。</param>
/// <param name="StartedFrom">開始日時の下限（UTC、含む）。未指定は絞らない。</param>
/// <param name="StartedTo">開始日時の上限（UTC、含まない）。未指定は絞らない。</param>
/// <param name="Waiting">待機中だけ（<see langword="true"/>）か、待機中でないものだけ（<see langword="false"/>）。未指定は絞らない。</param>
/// <param name="NeedsAttention">要対応（待機中または失敗）だけ（<see langword="true"/>）か、それ以外だけ（<see langword="false"/>）。未指定は絞らない。</param>
public sealed record ExecutionListPageQuery(
    PageQuery Page,
    SortQuery Sort,
    string? StatusFilter,
    Guid? DefinitionIdFilter,
    string? NameContains,
    Guid? ProjectId = null,
    DateTime? StartedFrom = null,
    DateTime? StartedTo = null,
    bool? Waiting = null,
    bool? NeedsAttention = null);

/// <summary>実行の件数の読み取りの絞り込み。意味は <see cref="ExecutionListPageQuery"/> の同名の項目と同じ。</summary>
/// <remarks>状態・待機・要対応では絞らない。件数はそれらごとに数えて返すため。</remarks>
/// <param name="DefinitionIdFilter">定義 UUID。未指定は絞らない。</param>
/// <param name="NameContains">表示 ID の部分一致、または execution UUID の完全一致。</param>
/// <param name="ProjectId">指定時はその Project の定義に紐づく実行だけ。</param>
/// <param name="StartedFrom">開始日時の下限（UTC、含む）。</param>
/// <param name="StartedTo">開始日時の上限（UTC、含まない）。</param>
public sealed record ExecutionCountFilter(
    Guid? DefinitionIdFilter,
    string? NameContains,
    Guid? ProjectId,
    DateTime? StartedFrom,
    DateTime? StartedTo);

/// <summary>実行の件数（ルートの実行だけを数える）。</summary>
/// <param name="Total">全体の件数。</param>
/// <param name="Running">投影の値が <c>Running</c> の件数。</param>
/// <param name="Completed">投影の値が <c>Completed</c> の件数。</param>
/// <param name="Failed">投影の値が <c>Failed</c> の件数。</param>
/// <param name="Cancelled">投影の値が <c>Cancelled</c> の件数。</param>
/// <param name="Waiting">待機中の件数。</param>
/// <param name="NeedsAttention">要対応（待機中または失敗）の件数。</param>
public sealed record ExecutionCounts(
    int Total,
    int Running,
    int Completed,
    int Failed,
    int Cancelled,
    int Waiting,
    int NeedsAttention);

/// <summary>定義ごとの最後のルートの実行。</summary>
/// <param name="DefinitionId">定義 UUID。</param>
/// <param name="ExecutionId">実行 UUID。</param>
/// <param name="DisplayId">実行の表示 ID。採番されていなければ <see langword="null"/>。</param>
/// <param name="Status">投影の値（<c>executions.status</c>）。</param>
/// <param name="StartedAt">開始日時（UTC）。</param>
/// <param name="UpdatedAt">最終更新日時（UTC）。</param>
/// <param name="Waiting">待機中か。</param>
public sealed record LatestRootExecution(
    Guid DefinitionId,
    Guid ExecutionId,
    string? DisplayId,
    string Status,
    DateTime StartedAt,
    DateTime UpdatedAt,
    bool Waiting);
