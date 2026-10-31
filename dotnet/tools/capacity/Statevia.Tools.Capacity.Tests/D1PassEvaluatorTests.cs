using Xunit;

namespace Statevia.Tools.Capacity.Tests;

/// <summary>D1 合否判定。</summary>
public sealed class D1PassEvaluatorTests
{
    /// <summary>再起動・ヘルス・投影・Resume・完了が揃えば合格。</summary>
    [Fact]
    public void IsPass_AllGreen_ReturnsTrue()
    {
        var actual = D1PassEvaluator.IsPass(
            restartSucceeded: true,
            healthRestored: true,
            projectionErrorCount: 0,
            resumeErrorCount: 0,
            completedCount: 8,
            acceptedCount: 8,
            failedCount: 0);

        Assert.True(actual);
    }

    /// <summary>投影エラーがあれば不合格。</summary>
    [Fact]
    public void IsPass_ProjectionError_ReturnsFalse()
    {
        var actual = D1PassEvaluator.IsPass(
            restartSucceeded: true,
            healthRestored: true,
            projectionErrorCount: 1,
            resumeErrorCount: 0,
            completedCount: 8,
            acceptedCount: 8,
            failedCount: 0);

        Assert.False(actual);
    }

    /// <summary>Start 受理ゼロは不合格。</summary>
    [Fact]
    public void IsPass_NoAcceptedStarts_ReturnsFalse()
    {
        var actual = D1PassEvaluator.IsPass(
            restartSucceeded: true,
            healthRestored: true,
            projectionErrorCount: 0,
            resumeErrorCount: 0,
            completedCount: 0,
            acceptedCount: 0,
            failedCount: 0);

        Assert.False(actual);
    }
}
