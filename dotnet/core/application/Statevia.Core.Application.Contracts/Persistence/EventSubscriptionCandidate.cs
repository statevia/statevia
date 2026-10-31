namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>テナント内で一意な購読候補。実行 ID・ノード ID・内部イベント名は持たない。</summary>
/// <param name="Topic">購読トピック。</param>
/// <param name="Key">相関キー。未指定の購読は空文字。</param>
public sealed record EventSubscriptionCandidate(string Topic, string Key);
