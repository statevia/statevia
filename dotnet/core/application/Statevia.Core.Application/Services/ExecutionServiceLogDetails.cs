namespace Statevia.Core.Application.Services;

/// <summary>
/// Serializable 永続化リトライログ用の構造化プロパティ。
/// </summary>
internal sealed class SerializablePersistRetryDetails
{
    /// <summary>相関に使うトレース ID。</summary>
    public required string TraceId { get; init; }

    /// <summary>Execution ID。</summary>
    public Guid ExecutionId { get; init; }

    /// <summary>テナントの内部 ID。</summary>
    public Guid TenantId { get; init; }

    /// <summary>クライアントイベント ID。</summary>
    public Guid ClientEventId { get; init; }

    /// <summary>何回目の試行か。</summary>
    public int Attempt { get; init; }

    /// <summary>試行の上限回数。</summary>
    public int MaxAttempts { get; init; }

    /// <summary>次の試行までの待機時間（ミリ秒）。</summary>
    public int DelayMs { get; init; }

    /// <summary>失敗した例外のメッセージ。</summary>
    public required string FailureMessage { get; init; }
}

/// <summary>
/// イベント配送 dedup 決定ログ用の構造化プロパティ。
/// </summary>
internal sealed class EventDeliveryDecisionDetails
{
    /// <summary>相関に使うトレース ID。</summary>
    public required string TraceId { get; init; }

    /// <summary>Execution ID。</summary>
    public Guid ExecutionId { get; init; }

    /// <summary>テナントの内部 ID。</summary>
    public Guid TenantId { get; init; }

    /// <summary>クライアントイベント ID。</summary>
    public Guid ClientEventId { get; init; }

    /// <summary>dedup の判定結果。</summary>
    public required string Decision { get; init; }

    /// <summary>何回目の試行か。</summary>
    public int Attempt { get; init; }

    /// <summary>判定までにかかった時間（ミリ秒）。</summary>
    public long ElapsedMs { get; init; }

    /// <summary>エラーコード。</summary>
    public required string ErrorCode { get; init; }
}
