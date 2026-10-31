using Xunit;

namespace Statevia.Tools.Capacity.Tests;

/// <summary>API エラー本文の解釈。</summary>
public sealed class CapacityApiErrorParserTests
{
    /// <summary>data-integration 形式の error.code / error.message を取る。</summary>
    [Fact]
    public void Parse_ApiErrorEnvelope_ReturnsCodeAndMessage()
    {
        const string body = """{"error":{"code":"VALIDATION_ERROR","message":"Node 'n1' is not an active Wait node in execution 'e1'."}}""";

        var (code, message) = CapacityApiErrorParser.Parse(body);

        Assert.Equal("VALIDATION_ERROR", code);
        Assert.Equal("Node 'n1' is not an active Wait node in execution 'e1'.", message);
    }

    /// <summary>ASP.NET の title/status をフォールバックとして使う。</summary>
    [Fact]
    public void Parse_ProblemDetails_ReturnsStatusAndTitle()
    {
        const string body = """{"title":"One or more validation errors occurred.","status":400}""";

        var (code, message) = CapacityApiErrorParser.Parse(body);

        Assert.Equal("400", code);
        Assert.Equal("One or more validation errors occurred.", message);
    }

    /// <summary>Bearer を含む本文は残さない。</summary>
    [Fact]
    public void Parse_BearerText_IsRedacted()
    {
        const string body = """{"error":{"code":"UNAUTHORIZED","message":"Bearer abc.def"}}""";

        var (code, message) = CapacityApiErrorParser.Parse(body);

        Assert.Equal("UNAUTHORIZED", code);
        Assert.Equal("[redacted]", message);
    }
}
