using Statevia.Core.Application.Contracts.Validation;

namespace Statevia.Service.Api.Tests.Contracts.Validation;

/// <summary><see cref="MaxUtf8BytesAttribute"/> の検証シナリオ。</summary>
public sealed class MaxUtf8BytesAttributeTests
{
    /// <summary>null は必須属性に委譲するため成功扱い。</summary>
    [Fact]
    public void IsValid_WhenNull_ReturnsTrue()
    {
        // Arrange
        var attribute = new MaxUtf8BytesAttribute(4);

        // Act
        var valid = attribute.IsValid(null);

        // Assert
        Assert.True(valid);
    }

    /// <summary>上限ちょうどのバイト数は成功する。</summary>
    [Fact]
    public void IsValid_WhenAtMaxBytes_ReturnsTrue()
    {
        // Arrange
        var attribute = new MaxUtf8BytesAttribute(4);

        // Act
        var valid = attribute.IsValid("abcd");

        // Assert
        Assert.True(valid);
    }

    /// <summary>上限を 1 バイト超えると失敗する。</summary>
    [Fact]
    public void IsValid_WhenExceedsMaxBytes_ReturnsFalse()
    {
        // Arrange
        var attribute = new MaxUtf8BytesAttribute(4);

        // Act
        var valid = attribute.IsValid("abcde");

        // Assert
        Assert.False(valid);
    }

    /// <summary>文字数が上限以内でも、マルチバイト文字で UTF-8 バイト数が超えると失敗する。</summary>
    [Fact]
    public void IsValid_WhenMultibyteExceedsMaxBytes_ReturnsFalse()
    {
        // Arrange
        var attribute = new MaxUtf8BytesAttribute(4);

        // Act（「定義」は 2 文字・UTF-8 で 6 バイト）
        var valid = attribute.IsValid("定義");

        // Assert
        Assert.False(valid);
    }

    /// <summary>string 以外は対象外として成功する。</summary>
    [Fact]
    public void IsValid_WhenNonString_ReturnsTrue()
    {
        // Arrange
        var attribute = new MaxUtf8BytesAttribute(4);

        // Act
        var valid = attribute.IsValid(123456);

        // Assert
        Assert.True(valid);
    }

    /// <summary>0 以下の上限は構成誤りとして拒否する。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxBytesNotPositive_Throws(int maxBytes)
    {
        // Arrange / Act
        var exception = Record.Exception(() => new MaxUtf8BytesAttribute(maxBytes));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }
}
