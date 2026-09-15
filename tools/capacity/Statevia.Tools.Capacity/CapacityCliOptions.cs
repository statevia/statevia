namespace Statevia.Tools.Capacity;

/// <summary>ハーネス起動オプション。</summary>
internal sealed class CapacityCliOptions
{
    /// <summary>シナリオ ID（L1 / L2 / L3 / D1 / C1）。</summary>
    public required string ScenarioId { get; init; }

    /// <summary>Service API の基点 URL。</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>テナントキー。</summary>
    public required string TenantKey { get; init; }

    /// <summary>ログインユーザー名。トークンまたは API キー指定時は未使用。</summary>
    public string? Username { get; init; }

    /// <summary>ログインパスワード。ログに出さない。</summary>
    public string? Password { get; init; }

    /// <summary>Bearer トークン。ログに出さない。</summary>
    public string? AccessToken { get; init; }

    /// <summary>API キー。ログに出さない。</summary>
    public string? ApiKey { get; init; }

    /// <summary>開始する実行数。</summary>
    public int Count { get; init; }

    /// <summary>同時 HTTP 数。</summary>
    public int Concurrency { get; init; }

    /// <summary>投影待ちのポーリング間隔。</summary>
    public TimeSpan PollInterval { get; init; }

    /// <summary>シナリオ全体の上限。</summary>
    public TimeSpan Timeout { get; init; }

    /// <summary>JSON 出力パス。</summary>
    public required string OutputPath { get; init; }

    /// <summary>HW ラベル。</summary>
    public required string HwLabel { get; init; }

    /// <summary>実効リソースなどの自由記述。</summary>
    public required string HwNotes { get; init; }

    /// <summary>トポロジ ID。</summary>
    public required string Topology { get; init; }

    /// <summary>compose 再起動対象のサービス名。</summary>
    public required string RestartServiceName { get; init; }

    /// <summary>記録用。Worker プロセス数。0 は未指定。</summary>
    public int WorkerReplicas { get; init; }

    /// <summary>記録用。プロセス内同時スロット。0 は未指定。</summary>
    public int WorkerMaxConcurrency { get; init; }

    /// <summary>記録用。Cancel ループ数。0 は未指定。</summary>
    public int WorkerCancelConcurrency { get; init; }

    /// <summary>C1 で Start 後に Cancel するまでの待ち。</summary>
    public TimeSpan SettleDelay { get; init; }
}
