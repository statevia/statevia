namespace Statevia.Tools.Capacity;

/// <summary>L2 Resume の非 2xx をステータス別件数と代表メッセージにまとめる。</summary>
internal static class ResumeFailureSummary
{
    /// <summary>メトリクスへ HTTP ステータス件数を載せる。</summary>
    /// <param name="metrics">追記先。</param>
    /// <param name="attempts">Resume 試行。</param>
    public static void ApplyToMetrics(
        IDictionary<string, double> metrics,
        IReadOnlyList<CapacityApi.ResumeAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(attempts);

        metrics["resumeHttp400Count"] = attempts.Count(static item => item.StatusCode == 400);
        metrics["resumeHttp404Count"] = attempts.Count(static item => item.StatusCode == 404);
        metrics["resumeHttp409Count"] = attempts.Count(static item => item.StatusCode == 409);
        metrics["resumeHttp422Count"] = attempts.Count(static item => item.StatusCode == 422);
        metrics["resumeHttpOther4xxCount"] = attempts.Count(static item =>
            item.StatusCode is >= 400 and < 500
            && item.StatusCode is not 400 and not 404 and not 409 and not 422);
    }

    /// <summary>一意な失敗メッセージを件数付きで連結する。秘密はパーサ側で落としている。</summary>
    /// <param name="attempts">Resume 試行。</param>
    /// <param name="maxGroups">載せるグループ数の上限。</param>
    /// <returns>空なら空文字。</returns>
    public static string FormatSamples(IReadOnlyList<CapacityApi.ResumeAttempt> attempts, int maxGroups = 5)
    {
        ArgumentNullException.ThrowIfNull(attempts);

        var groups = attempts
            .Where(static item => item.StatusCode is < 200 or >= 300)
            .GroupBy(static item => $"{item.StatusCode}|{item.ErrorCode}|{item.ErrorMessage}", StringComparer.Ordinal)
            .Select(static group =>
            {
                var sample = group.First();
                var code = string.IsNullOrEmpty(sample.ErrorCode) ? "-" : sample.ErrorCode;
                var message = string.IsNullOrEmpty(sample.ErrorMessage) ? "-" : sample.ErrorMessage;
                return $"{sample.StatusCode} {code} {message} (n={group.Count()})";
            })
            .Take(maxGroups)
            .ToArray();

        return string.Join(" | ", groups);
    }
}
