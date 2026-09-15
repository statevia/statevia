using Xunit;

namespace Statevia.Tools.Capacity.Tests;

/// <summary>分位点とレート計算の検証。</summary>
public sealed class LatencyStatsTests
{
    /// <summary>空サンプルは null を返す。</summary>
    [Fact]
    public void PercentileNearestRank_Empty_ReturnsNull()
    {
        var actual = LatencyStats.PercentileNearestRank([], 95);

        Assert.Null(actual);
    }

    /// <summary>範囲外のパーセンタイルは例外にする。</summary>
    [Fact]
    public void PercentileNearestRank_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LatencyStats.PercentileNearestRank([1], 101));
    }

    /// <summary>既知の 20 点で p50 / p95 が nearest-rank になる。</summary>
    [Fact]
    public void PercentileNearestRank_TwentySamples_MatchesNearestRank()
    {
        var samples = Enumerable.Range(1, 20).Select(static value => (double)value).ToArray();

        var p50 = LatencyStats.PercentileNearestRank(samples, 50);
        var p95 = LatencyStats.PercentileNearestRank(samples, 95);

        Assert.Equal(10, p50);
        Assert.Equal(19, p95);
    }

    /// <summary>経過 0 のレートは null。</summary>
    [Fact]
    public void RatePerSecond_ZeroElapsed_ReturnsNull()
    {
        var actual = LatencyStats.RatePerSecond(10, TimeSpan.Zero);

        Assert.Null(actual);
    }

    /// <summary>10 件を 2 秒で割ると 5。</summary>
    [Fact]
    public void RatePerSecond_TenOverTwoSeconds_IsFive()
    {
        var actual = LatencyStats.RatePerSecond(10, TimeSpan.FromSeconds(2));

        Assert.Equal(5, actual);
    }
}
