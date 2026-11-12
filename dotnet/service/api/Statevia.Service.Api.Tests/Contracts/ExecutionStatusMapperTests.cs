namespace Statevia.Service.Api.Tests.Contracts;

/// <summary>実行ステータスの投影の値と契約の値の変換。</summary>
public sealed class ExecutionStatusMapperTests
{
    /// <summary>投影の 4 値は、それぞれ契約の値になる。</summary>
    [Theory]
    [InlineData("Running", "ACTIVE")]
    [InlineData("Completed", "COMPLETED")]
    [InlineData("Failed", "FAILED")]
    [InlineData("Cancelled", "CANCELED")]
    public void ToContract_WhenKnownProjectionStatus_ReturnsContractStatus(string projectionStatus, string expected)
    {
        // Arrange & Act
        var contractStatus = ExecutionStatusMapper.ToContract(projectionStatus);

        // Assert
        Assert.Equal(expected, contractStatus);
    }

    /// <summary>想定外の値（投影の Unknown、契約の値、null を含む）は UNKNOWN になり、受けた値をそのまま返さない。</summary>
    [Theory]
    [InlineData("Unknown")]
    [InlineData("ACTIVE")]
    [InlineData("running")]
    [InlineData("")]
    [InlineData(null)]
    public void ToContract_WhenUnexpectedValue_ReturnsUnknown(string? projectionStatus)
    {
        // Arrange & Act
        var contractStatus = ExecutionStatusMapper.ToContract(projectionStatus);

        // Assert
        Assert.Equal(ExecutionContractStatuses.Unknown, contractStatus);
    }

    /// <summary>フィルタの 4 つの許可値は、投影の値に戻せる。</summary>
    [Theory]
    [InlineData("ACTIVE", "Running")]
    [InlineData("COMPLETED", "Completed")]
    [InlineData("FAILED", "Failed")]
    [InlineData("CANCELED", "Cancelled")]
    public void TryToProjection_WhenFilterableContractStatus_ReturnsProjectionStatus(string contractStatus, string expected)
    {
        // Arrange & Act
        var converted = ExecutionStatusMapper.TryToProjection(contractStatus, out var projectionStatus);

        // Assert
        Assert.True(converted);
        Assert.Equal(expected, projectionStatus);
    }

    /// <summary>UNKNOWN・投影の値・小文字・それ以外は戻せない。</summary>
    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("Running")]
    [InlineData("Cancelled")]
    [InlineData("active")]
    [InlineData("CANCELLED")]
    [InlineData("")]
    [InlineData(null)]
    public void TryToProjection_WhenNotFilterable_ReturnsFalse(string? contractStatus)
    {
        // Arrange & Act
        var converted = ExecutionStatusMapper.TryToProjection(contractStatus, out var projectionStatus);

        // Assert
        Assert.False(converted);
        Assert.Equal("", projectionStatus);
    }
}
