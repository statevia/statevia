namespace Statevia.Core.Engine.Abstractions;

/// <summary>システムスコープのドメインイベント（publish / 外部 bus 連携の要約）。</summary>
/// <param name="Topic">トピック名。</param>
/// <param name="PayloadSummary">ペイロード要約（機微値を含めない）。</param>
public sealed record DomainEvent(string Topic, object? PayloadSummary);
