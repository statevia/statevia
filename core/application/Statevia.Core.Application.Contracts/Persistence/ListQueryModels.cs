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
/// <param name="StatusFilter">ステータス。未指定は絞らない。</param>
/// <param name="DefinitionIdFilter">定義 UUID。未指定は絞らない。</param>
/// <param name="NameContains">表示 ID の部分一致、または execution UUID の完全一致。</param>
/// <param name="ProjectId">指定時はその Project の定義に紐づく実行だけ。未所属の Project は 0 件。未指定は所属の和。</param>
public sealed record ExecutionListPageQuery(
    PageQuery Page,
    SortQuery Sort,
    string? StatusFilter,
    Guid? DefinitionIdFilter,
    string? NameContains,
    Guid? ProjectId = null);
