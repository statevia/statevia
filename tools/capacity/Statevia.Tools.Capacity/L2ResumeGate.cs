namespace Statevia.Tools.Capacity;

/// <summary>L2 で Resume を送る前に、投影 status から対象外を判定する。</summary>
/// <remarks>
/// GET /waits の graph は終端後もしばらく WAITING を返すことがある。
/// 終端済みへ Resume すると hydrate 422 になり、Wait 容量の失敗と誤認する。
/// </remarks>
internal static class L2ResumeGate
{
    /// <summary>すでに完了している。Resume は送らずスキップする。</summary>
    /// <param name="status">GET execution の status。null は未取得。</param>
    /// <returns>Completed なら true。</returns>
    public static bool IsCompleted(string? status)
        => string.Equals(status, "Completed", StringComparison.Ordinal);

    /// <summary>失敗またはキャンセル。Resume しない。</summary>
    /// <param name="status">GET execution の status。</param>
    /// <returns>Failed / Cancelled なら true。</returns>
    public static bool IsFailedOrCancelled(string? status)
        => status is "Failed" or "Cancelled";

    /// <summary>終端状態。待ちポーリングを打ち切る。</summary>
    /// <param name="status">GET execution の status。</param>
    /// <returns>Completed / Failed / Cancelled なら true。</returns>
    public static bool IsTerminal(string? status)
        => IsCompleted(status) || IsFailedOrCancelled(status);

    /// <summary>この status では Resume HTTP を送らない。</summary>
    /// <param name="status">GET execution の status。</param>
    /// <returns>終端なら true。</returns>
    public static bool ShouldSkipResume(string? status)
        => IsTerminal(status);
}
