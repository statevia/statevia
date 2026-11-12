using Xunit;

namespace Statevia.Tools.Capacity.Tests;

/// <summary>L2 Resume 対象外の判定。</summary>
public sealed class L2ResumeGateTests
{
    /// <summary>COMPLETED は Resume しない。</summary>
    [Fact]
    public void ShouldSkipResume_Completed_IsTrue()
    {
        Assert.True(L2ResumeGate.ShouldSkipResume("COMPLETED"));
        Assert.True(L2ResumeGate.IsCompleted("COMPLETED"));
    }

    /// <summary>FAILED / CANCELED も Resume しない。</summary>
    [Fact]
    public void ShouldSkipResume_FailedOrCancelled_IsTrue()
    {
        Assert.True(L2ResumeGate.ShouldSkipResume("FAILED"));
        Assert.True(L2ResumeGate.ShouldSkipResume("CANCELED"));
        Assert.True(L2ResumeGate.IsFailedOrCancelled("FAILED"));
    }

    /// <summary>WAITING / ACTIVE 相当は Resume 対象のままにする。</summary>
    [Fact]
    public void ShouldSkipResume_WaitingOrNull_IsFalse()
    {
        Assert.False(L2ResumeGate.ShouldSkipResume("WAITING"));
        Assert.False(L2ResumeGate.ShouldSkipResume("ACTIVE"));
        Assert.False(L2ResumeGate.ShouldSkipResume(null));
        Assert.False(L2ResumeGate.IsTerminal("WAITING"));
    }
}
