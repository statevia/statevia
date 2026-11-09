namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>command_dedup テーブル（コマンド冪等制御）。</summary>
public class CommandDedupRow
{
    /// <summary>冪等キーの合成キー（テナント・エンドポイント・冪等キー・本文ハッシュ）。</summary>
    public required string DedupKey { get; set; }
    /// <summary>HTTP メソッドとパス。</summary>
    public required string Endpoint { get; set; }
    /// <summary><c>X-Idempotency-Key</c> の値。</summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>リクエストボディのハッシュ（任意）。</summary>
    public string? RequestHash { get; set; }

    /// <summary>キャッシュした HTTP ステータス。</summary>
    public int? StatusCode { get; set; }
    /// <summary>キャッシュしたレスポンス本文。</summary>
    public string? ResponseBody { get; set; }

    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>有効期限（UTC）。過ぎた記録は使わない。</summary>
    public DateTime ExpiresAt { get; set; }
}
