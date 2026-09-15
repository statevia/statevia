namespace Statevia.Tools.Capacity;

/// <summary>D1 再起動耐久の合否。スループットは見ない。</summary>
internal static class D1PassEvaluator
{
    /// <summary>再起動後に Resume でき、投影が壊れていないときだけ合格。</summary>
    /// <param name="restartSucceeded">compose restart が exit 0。</param>
    /// <param name="healthRestored">再起動後に /v1/health が 2xx。</param>
    /// <param name="projectionErrorCount">GET execution / graph / waits の異常件数。</param>
    /// <param name="resumeErrorCount">Resume 非 2xx 件数。</param>
    /// <param name="completedCount">Completed 件数。</param>
    /// <param name="acceptedCount">Start 受理件数。</param>
    /// <param name="failedCount">Failed 件数。</param>
    /// <returns>合格なら true。</returns>
    public static bool IsPass(
        bool restartSucceeded,
        bool healthRestored,
        int projectionErrorCount,
        int resumeErrorCount,
        int completedCount,
        int acceptedCount,
        int failedCount)
    {
        return restartSucceeded
            && healthRestored
            && projectionErrorCount == 0
            && resumeErrorCount == 0
            && failedCount == 0
            && acceptedCount > 0
            && completedCount == acceptedCount;
    }
}
