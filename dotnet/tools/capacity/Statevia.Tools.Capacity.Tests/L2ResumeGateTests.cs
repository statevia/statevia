using Xunit;

namespace Statevia.Tools.Capacity.Tests;

/// <summary>L2 Resume 対象外の判定。</summary>
public sealed class L2ResumeGateTests
{
    /// <summary>Completed は Resume しない。</summary>
    [Fact]
    public void ShouldSkipResume_Completed_IsTrue()
    {
        Assert.True(L2ResumeGate.ShouldSkipResume("Completed"));
        Assert.True(L2ResumeGate.IsCompleted("Completed"));
    }

    /// <summary>Failed / Cancelled も Resume しない。</summary>
    [Fact]
    public void ShouldSkipResume_FailedOrCancelled_IsTrue()
    {
        Assert.True(L2ResumeGate.ShouldSkipResume("Failed"));
        Assert.True(L2ResumeGate.ShouldSkipResume("Cancelled"));
        Assert.True(L2ResumeGate.IsFailedOrCancelled("Failed"));
    }

    /// <summary>WAITING / Running 相当は Resume 対象のままにする。</summary>
    [Fact]
    public void ShouldSkipResume_WaitingOrNull_IsFalse()
    {
        Assert.False(L2ResumeGate.ShouldSkipResume("WAITING"));
        Assert.False(L2ResumeGate.ShouldSkipResume("Running"));
        Assert.False(L2ResumeGate.ShouldSkipResume(null));
        Assert.False(L2ResumeGate.IsTerminal("WAITING"));
    }
}
