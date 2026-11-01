using Statevia.Core.Engine.Definition;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary><see cref="ExecutionContextReadPath.Classify"/> の判定。</summary>
public sealed class ExecutionContextReadPathTests
{
    /// <summary>許可された CallPath と SimpleJsonPath は受理する。</summary>
    [Theory]
    [InlineData("$.sys.now(\"yyyyMMdd\")")]
    [InlineData("$.sys.utcNow(\"HHmm\")")]
    [InlineData("$.vars.a")]
    [InlineData("$.sys.now")]
    [InlineData("$.states['a.b'].output")]
    [InlineData("$.input.items[0]")]
    public void Classify_AllowedPath_ReturnsValid(string path)
    {
        // Act
        var validity = ExecutionContextReadPath.Classify(path);

        // Assert
        Assert.Equal(ReadPathValidity.Valid, validity);
    }

    /// <summary>CallPath の形だが許可されていないものは InvalidCallPath にする。</summary>
    [Theory]
    [InlineData("$.sys.today(\"yyyyMMdd\")")]
    [InlineData("$.sys.now('yyyyMMdd')")]
    [InlineData("$.sys.now()")]
    [InlineData("$.sys.now(\"a\", \"b\")")]
    [InlineData("$.sys.now(\"yyyyMMdd\").length")]
    public void Classify_InvalidCallPath_ReturnsInvalidCallPath(string path)
    {
        // Act
        var validity = ExecutionContextReadPath.Classify(path);

        // Assert
        Assert.Equal(ReadPathValidity.InvalidCallPath, validity);
    }

    /// <summary>引数が上限を超える CallPath は InvalidCallPath にする。</summary>
    [Fact]
    public void Classify_TooLongCallPathArgument_ReturnsInvalidCallPath()
    {
        // Arrange
        var path = $"$.sys.now(\"{new string('y', SysPathCall.MaxArgumentLength + 1)}\")";

        // Act
        var validity = ExecutionContextReadPath.Classify(path);

        // Assert
        Assert.Equal(ReadPathValidity.InvalidCallPath, validity);
    }

    /// <summary>SimpleJsonPath として不正なパスは InvalidPath にする。</summary>
    [Theory]
    [InlineData("$.vars[*]")]
    [InlineData("$.vars[-1]")]
    [InlineData("$.")]
    public void Classify_InvalidSimpleJsonPath_ReturnsInvalidPath(string path)
    {
        // Act
        var validity = ExecutionContextReadPath.Classify(path);

        // Assert
        Assert.Equal(ReadPathValidity.InvalidPath, validity);
    }
}
