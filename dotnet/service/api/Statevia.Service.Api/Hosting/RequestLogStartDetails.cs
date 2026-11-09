namespace Statevia.Service.Api.Hosting;

/// <summary>
/// HTTP リクエスト開始ログ用の構造化プロパティ。
/// </summary>
internal sealed class RequestLogStartDetails
{
    /// <summary>相関に使うトレース ID。</summary>
    public required string TraceId { get; init; }

    /// <summary>HTTP メソッド。</summary>
    public required string Method { get; init; }

    /// <summary>リクエストのパス。</summary>
    public required string Path { get; init; }

    /// <summary>ログ用にマスクしたクエリ文字列。</summary>
    public required string QueryForLog { get; init; }

    /// <summary>テナントの内部 ID。未解決なら null。</summary>
    public Guid? TenantId { get; init; }

    /// <summary>User-Agent。無ければ null。</summary>
    public string? UserAgent { get; init; }

    /// <summary>ログ用にマスクしたリクエスト本文。記録しないときは null。</summary>
    public string? RequestBody { get; init; }
}
