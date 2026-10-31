namespace Statevia.Service.Api.Contracts;

/// <summary>現在テナントの購読候補一覧。</summary>
/// <remarks>displayId、nodeId、内部イベント名、payload は含めない。</remarks>
public sealed class EventSubscriptionListResponse
{
    /// <summary>一意な topic / key。最大 500 件。topic、key の昇順。</summary>
    public required IReadOnlyList<EventSubscriptionCandidateResponse> Subscriptions { get; init; }
}

/// <summary>発行時に使える topic / key の 1 行。</summary>
public sealed class EventSubscriptionCandidateResponse
{
    /// <summary>購読トピック。</summary>
    public required string Topic { get; init; }

    /// <summary>相関キー。未指定の購読は空文字。</summary>
    public required string Key { get; init; }
}
