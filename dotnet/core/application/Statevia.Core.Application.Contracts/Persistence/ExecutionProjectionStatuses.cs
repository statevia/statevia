namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <c>executions.status</c> および実行投影で用いるワークフロー実行状態の文字列定数。
/// </summary>
/// <remarks>
/// <para>
/// Engine スナップショット（<c>IsCompleted</c> / <c>IsCancelled</c> / <c>IsFailed</c>）から
/// Application が写像する内部投影値。HTTP の契約の値（<see cref="ExecutionContractStatuses"/>）への変換は
/// <see cref="ExecutionStatusMapper"/> だけが行う。HTTP に投影の値は出さない。
/// </para>
/// </remarks>
public static class ExecutionProjectionStatuses
{
    /// <summary>実行中（終端でない）。</summary>
    public const string Running = "Running";

    /// <summary>正常完了。</summary>
    public const string Completed = "Completed";

    /// <summary>失敗終了。</summary>
    public const string Failed = "Failed";

    /// <summary>キャンセル終了。</summary>
    public const string Cancelled = "Cancelled";

    /// <summary>スナップショット欠落など、状態を判定できないとき。</summary>
    public const string Unknown = "Unknown";

    /// <summary>終端状態（Completed / Cancelled / Failed）かどうか。</summary>
    /// <param name="status">投影ステータス文字列。</param>
    /// <returns>終端なら <see langword="true"/>。</returns>
    public static bool IsTerminal(string status) =>
        status is Completed or Cancelled or Failed;
}
