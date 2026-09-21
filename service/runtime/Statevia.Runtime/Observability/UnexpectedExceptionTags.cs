namespace Statevia.Runtime.Observability;

/// <summary>
/// 想定外例外に付ける相関タグ。欠けているキーは付けない。
/// </summary>
/// <param name="TraceId">API の相関 ID。Worker では通常欠落。</param>
/// <param name="TenantId">解決済みテナント UUID。</param>
/// <param name="ExecutionId">実行 ID。表示 ID または UUID 文字列。</param>
/// <param name="WorkItemId">work item UUID。Worker のみ。</param>
public readonly record struct UnexpectedExceptionTags(
    string? TraceId = null,
    Guid? TenantId = null,
    string? ExecutionId = null,
    Guid? WorkItemId = null);
