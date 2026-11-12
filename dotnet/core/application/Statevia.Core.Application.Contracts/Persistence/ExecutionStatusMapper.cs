namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// 実行ステータスを、投影の値（<see cref="ExecutionProjectionStatuses"/>）と契約の値（<see cref="ExecutionContractStatuses"/>）の間で変換する。
/// </summary>
/// <remarks>
/// <para>
/// 変換はここだけで行う。応答は DTO に詰める直前に <see cref="ToContract"/> で契約の値にし、
/// <c>status</c> フィルタは <see cref="TryToProjection"/> で投影の値に戻してから DB に渡す。
/// 内部の判定（<see cref="ExecutionProjectionStatuses.IsTerminal"/> など）は投影の値のまま使う。
/// </para>
/// <para>
/// フィルタで受けるのは <c>ACTIVE</c> / <c>COMPLETED</c> / <c>FAILED</c> / <c>CANCELED</c> の 4 つだけである。
/// 投影の値（<c>Running</c> など）と <c>UNKNOWN</c> は受けない。開発中は互換のためのフォールバックを作らない。
/// </para>
/// </remarks>
public static class ExecutionStatusMapper
{
    /// <summary>フィルタで受ける契約の値と、それに対応する投影の値。</summary>
    private static readonly Dictionary<string, string> ContractToProjection = new(StringComparer.Ordinal)
    {
        [ExecutionContractStatuses.Active] = ExecutionProjectionStatuses.Running,
        [ExecutionContractStatuses.Completed] = ExecutionProjectionStatuses.Completed,
        [ExecutionContractStatuses.Failed] = ExecutionProjectionStatuses.Failed,
        [ExecutionContractStatuses.Canceled] = ExecutionProjectionStatuses.Cancelled,
    };

    /// <summary>フィルタで受ける契約の値（エラーの説明に使う）。</summary>
    public static IReadOnlyCollection<string> FilterableContractStatuses { get; } =
    [
        ExecutionContractStatuses.Active,
        ExecutionContractStatuses.Completed,
        ExecutionContractStatuses.Failed,
        ExecutionContractStatuses.Canceled,
    ];

    /// <summary>投影の値を契約の値に変換する。</summary>
    /// <param name="projectionStatus">投影の値（<c>executions.status</c>）。</param>
    /// <returns>契約の値。想定外の値は <see cref="ExecutionContractStatuses.Unknown"/>。投影の値をそのまま返すことは無い。</returns>
    public static string ToContract(string? projectionStatus) =>
        projectionStatus switch
        {
            ExecutionProjectionStatuses.Running => ExecutionContractStatuses.Active,
            ExecutionProjectionStatuses.Completed => ExecutionContractStatuses.Completed,
            ExecutionProjectionStatuses.Failed => ExecutionContractStatuses.Failed,
            ExecutionProjectionStatuses.Cancelled => ExecutionContractStatuses.Canceled,
            _ => ExecutionContractStatuses.Unknown,
        };

    /// <summary>フィルタの契約の値を投影の値に戻す。</summary>
    /// <param name="contractStatus">クエリで受けた値。大文字小文字を区別する。</param>
    /// <param name="projectionStatus">戻した投影の値。戻せないときは空文字。</param>
    /// <returns>4 つの許可値のどれかなら <see langword="true"/>。<c>UNKNOWN</c>・投影の値・それ以外は <see langword="false"/>。</returns>
    public static bool TryToProjection(string? contractStatus, out string projectionStatus)
    {
        if (contractStatus is not null && ContractToProjection.TryGetValue(contractStatus, out var mapped))
        {
            projectionStatus = mapped;
            return true;
        }

        projectionStatus = "";
        return false;
    }
}
