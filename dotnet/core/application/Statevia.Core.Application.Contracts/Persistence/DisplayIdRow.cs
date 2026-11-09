namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>表示用 ID とリソース UUID の対応（display_ids テーブル）。</summary>
public class DisplayIdRow
{
    /// <summary>リソース種別（<c>definition</c> / <c>execution</c>）。</summary>
    public required string Kind { get; set; }
    /// <summary>表示と URL に使う短い ID（英数字 10 桁）。</summary>
    public required string DisplayId { get; set; }
    /// <summary>リソースの UUID（定義 ID または Execution ID）。</summary>
    public required Guid ResourceId { get; set; }
    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}
