using Statevia.Core.Application.Contracts.Validation;
using Statevia.Core.Engine.Definition.Validation;

namespace Statevia.Service.Api.Tests.Contracts.Validation;

/// <summary>Identifier / PathIdentifier 制約と Engine 定数の一致。</summary>
public sealed class IdentifierConstraintsTests
{
    /// <summary>許可文字は有効。</summary>
    [Theory]
    [InlineData("Order_v2")]
    [InlineData("start")]
    [InlineData("A")]
    [InlineData("Order_v2.start")]
    public void IsValid_WhenAllowedCharset_ReturnsTrue(string value)
    {
        // Arrange & Act
        var valid = IdentifierConstraints.IsValid(value, IdentifierConstraints.DefinitionNameMaxLength);

        // Assert
        Assert.True(valid);
    }

    /// <summary>日本語・全角・絵文字・記号・先頭ハイフン・空白は無効。</summary>
    [Theory]
    [InlineData("ユーザー")]
    [InlineData("Ａ")]
    [InlineData("😀")]
    [InlineData("<x>")]
    [InlineData("-ops")]
    [InlineData("")]
    [InlineData(" ")]
    public void IsValid_WhenDisallowed_ReturnsFalse(string value)
    {
        // Arrange & Act
        var valid = IdentifierConstraints.IsValid(value, IdentifierConstraints.DefinitionNameMaxLength);

        // Assert
        Assert.False(valid);
    }

    /// <summary>定義名は 100 文字まで有効、101 は無効。</summary>
    [Fact]
    public void IsValid_WhenLengthBoundary_EnforcesMaxLength()
    {
        // Arrange
        var atLimit = "A" + new string('a', IdentifierConstraints.DefinitionNameMaxLength - 1);
        var tooLong = "A" + new string('a', IdentifierConstraints.DefinitionNameMaxLength);

        // Act & Assert
        Assert.True(IdentifierConstraints.IsValid(atLimit, IdentifierConstraints.DefinitionNameMaxLength));
        Assert.False(IdentifierConstraints.IsValid(tooLong, IdentifierConstraints.DefinitionNameMaxLength));
    }

    /// <summary>Username / TenantKey の既存パターンを変更していない。</summary>
    [Fact]
    public void ExistingUsernamePattern_RemainsUnchanged()
    {
        // Arrange & Act & Assert
        Assert.Equal("^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$", UsernameConstraints.AllowedPattern);
        Assert.Equal(64, UsernameConstraints.MaxLength);
    }

    /// <summary>Engine の Identifier / PathIdentifier パターン文字列が Contracts と一致する。</summary>
    [Fact]
    public void EngineIdentifierCharset_MatchesContractsPatterns()
    {
        // Arrange & Act & Assert
        Assert.Equal(IdentifierConstraints.AllowedPattern, IdentifierCharset.AllowedPattern);
        Assert.Equal(IdentifierConstraints.PathIdentifierPattern, IdentifierCharset.PathIdentifierPattern);
    }
}
