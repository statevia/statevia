using Xunit;

namespace Statevia.Tools.Capacity.Tests;

/// <summary>Resume 失敗の件数集計。</summary>
public sealed class ResumeFailureSummaryTests
{
    /// <summary>422 と 409 を別カウンタに載せる。</summary>
    [Fact]
    public void ApplyToMetrics_CountsStatusBuckets()
    {
        var metrics = new Dictionary<string, double>(StringComparer.Ordinal);
        CapacityApi.ResumeAttempt[] attempts =
        [
            new(204, 10, Is5xx: false),
            new(422, 12, Is5xx: false, "VALIDATION_ERROR", "not an active Wait"),
            new(422, 11, Is5xx: false, "VALIDATION_ERROR", "not an active Wait"),
            new(409, 9, Is5xx: false, "STATE_CONFLICT", "conflict"),
        ];

        ResumeFailureSummary.ApplyToMetrics(metrics, attempts);

        Assert.Equal(0, metrics["resumeHttp400Count"]);
        Assert.Equal(0, metrics["resumeHttp404Count"]);
        Assert.Equal(1, metrics["resumeHttp409Count"]);
        Assert.Equal(2, metrics["resumeHttp422Count"]);
        Assert.Equal(0, metrics["resumeHttpOther4xxCount"]);
    }

    /// <summary>同一メッセージは件数にまとめる。</summary>
    [Fact]
    public void FormatSamples_GroupsIdenticalMessages()
    {
        CapacityApi.ResumeAttempt[] attempts =
        [
            new(422, 12, Is5xx: false, "VALIDATION_ERROR", "not an active Wait"),
            new(422, 11, Is5xx: false, "VALIDATION_ERROR", "not an active Wait"),
        ];

        var actual = ResumeFailureSummary.FormatSamples(attempts);

        Assert.Equal("422 VALIDATION_ERROR not an active Wait (n=2)", actual);
    }
}
