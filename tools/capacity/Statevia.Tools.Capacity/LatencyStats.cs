namespace Statevia.Tools.Capacity;

/// <summary>昇順に近いランク法でレイテンシ分位点を求める。</summary>
internal static class LatencyStats
{
    /// <summary>
    /// サンプルのパーセンタイル（ミリ秒）を返す。空は null。
    /// </summary>
    /// <param name="samplesMilliseconds">観測値（順不同）。</param>
    /// <param name="percentile">0〜100。</param>
    /// <returns>分位点。サンプルが無いとき null。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="percentile"/> が範囲外。</exception>
    public static double? PercentileNearestRank(IReadOnlyList<double> samplesMilliseconds, double percentile)
    {
        ArgumentNullException.ThrowIfNull(samplesMilliseconds);
        if (percentile is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "percentile must be 0..=100.");
        }

        if (samplesMilliseconds.Count == 0)
        {
            return null;
        }

        var sorted = samplesMilliseconds.OrderBy(static value => value).ToArray();
        if (percentile == 0)
        {
            return sorted[0];
        }

        var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Length) - 1;
        rank = Math.Clamp(rank, 0, sorted.Length - 1);
        return sorted[rank];
    }

    /// <summary>件数を経過秒で割ったレート。経過が 0 以下なら null。</summary>
    /// <param name="count">成功件数。</param>
    /// <param name="elapsed">経過時間。</param>
    /// <returns>1 秒あたりの件数。</returns>
    public static double? RatePerSecond(int count, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return null;
        }

        return count / elapsed.TotalSeconds;
    }
}
