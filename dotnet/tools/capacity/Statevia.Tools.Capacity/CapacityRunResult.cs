using System.Text.Json.Serialization;

namespace Statevia.Tools.Capacity;

/// <summary>1 回の容量計測結果（公開表の根拠。秘密は載せない）。</summary>
internal sealed class CapacityRunResult
{
    /// <summary>シナリオ ID。</summary>
    [JsonPropertyName("scenarioId")]
    public required string ScenarioId { get; init; }

    /// <summary>計測開始（UTC）。</summary>
    [JsonPropertyName("measuredAtUtc")]
    public required DateTimeOffset MeasuredAtUtc { get; init; }

    /// <summary>対象コードの git SHA。取得失敗時は空。</summary>
    [JsonPropertyName("gitSha")]
    public required string GitSha { get; init; }

    /// <summary>HW ラベル。</summary>
    [JsonPropertyName("hwLabel")]
    public required string HwLabel { get; init; }

    /// <summary>実効 vCPU / メモリなど。</summary>
    [JsonPropertyName("hwNotes")]
    public required string HwNotes { get; init; }

    /// <summary>トポロジ ID。</summary>
    [JsonPropertyName("topology")]
    public required string Topology { get; init; }

    /// <summary>観測メトリクス。</summary>
    [JsonPropertyName("metrics")]
    public required IReadOnlyDictionary<string, double> Metrics { get; init; }

    /// <summary>ランプで確定した上限。未確定は空。</summary>
    [JsonPropertyName("provisionalCap")]
    public required IReadOnlyDictionary<string, double> ProvisionalCap { get; init; }

    /// <summary>特記。</summary>
    [JsonPropertyName("notes")]
    public required string Notes { get; init; }
}
