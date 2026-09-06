using Statevia.Core.Application.Contracts.Validation;

namespace Statevia.Service.Api.Tests.Contracts.Validation;

/// <summary>AsciiLabel 制約。</summary>
public sealed class AsciiLabelConstraintsTests
{
    /// <summary>許可文字（スペース含む）は有効。</summary>
    [Theory]
    [InlineData("A")]
    [InlineData("Acme Corporation")]
    [InlineData("Ops.Team_1-lab")]
    public void IsValid_WhenAllowedCharset_ReturnsTrue(string value)
    {
        // Arrange & Act
        var valid = AsciiLabelConstraints.IsValid(value, AsciiLabelConstraints.DisplayNameMaxLength);

        // Assert
        Assert.True(valid);
    }

    /// <summary>日本語・絵文字・先頭末尾ハイフン/スペースは無効。</summary>
    [Theory]
    [InlineData("ユーザー")]
    [InlineData("😀")]
    [InlineData("-ops")]
    [InlineData("ops-")]
    [InlineData(" Acme")]
    [InlineData("Acme ")]
    [InlineData("")]
    public void IsValid_WhenDisallowed_ReturnsFalse(string value)
    {
        // Arrange & Act
        var valid = AsciiLabelConstraints.IsValid(value, AsciiLabelConstraints.DisplayNameMaxLength);

        // Assert
        Assert.False(valid);
    }

    /// <summary>256 文字は有効、257 文字は無効。</summary>
    [Fact]
    public void IsValid_WhenLengthBoundary_Enforces256()
    {
        // Arrange
        var atLimit = new string('A', AsciiLabelConstraints.DisplayNameMaxLength);
        var tooLong = new string('A', AsciiLabelConstraints.DisplayNameMaxLength + 1);

        // Act & Assert
        Assert.True(AsciiLabelConstraints.IsValid(atLimit, AsciiLabelConstraints.DisplayNameMaxLength));
        Assert.False(AsciiLabelConstraints.IsValid(tooLong, AsciiLabelConstraints.DisplayNameMaxLength));
    }
}
