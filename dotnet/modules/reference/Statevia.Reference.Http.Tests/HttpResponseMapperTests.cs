using System.Net;
using System.Text;

namespace Statevia.Reference.Http.Tests;

/// <summary>応答本文を UTF-8 のバイト数で上限へ切り詰める検証。</summary>
public sealed class HttpResponseMapperTests
{
    private const int MaxBodyBytes = HttpResponseMapper.MaxBodyBytes;

    /// <summary>
    /// 多バイト文字が多く、UTF-8 では上限を超えるが文字数は上限未満の本文を、例外なく上限以内へ切り詰める。
    /// </summary>
    [Fact]
    public async Task MapAsync_MultiByteBodyOverLimit_TruncatesWithinUtf8ByteLimit()
    {
        // Arrange
        var body = new string('あ', 600_000);
        using var response = CreateResponse(body);

        // Act
        var output = await HttpResponseMapper.MapAsync(response, CancellationToken.None);

        // Assert
        var mappedBody = Assert.IsType<string>(output["body"]);
        Assert.Equal(MaxBodyBytes / 3, mappedBody.Length);
        Assert.True(Encoding.UTF8.GetByteCount(mappedBody) <= MaxBodyBytes);
    }

    /// <summary>上限をまたぐサロゲートペアは途中で切らず、ペアごと落とす。</summary>
    [Fact]
    public async Task MapAsync_SurrogatePairAcrossLimit_DoesNotSplitPair()
    {
        // Arrange
        // 先頭 1 バイト + 絵文字（UTF-8 で 4 バイト）を並べ、上限の位置が絵文字の途中に来るようにする。
        var body = "a" + string.Concat(Enumerable.Repeat("😀", MaxBodyBytes / 4));
        using var response = CreateResponse(body);

        // Act
        var output = await HttpResponseMapper.MapAsync(response, CancellationToken.None);

        // Assert
        var mappedBody = Assert.IsType<string>(output["body"]);
        Assert.Equal(1 + ((MaxBodyBytes / 4) - 1) * 2, mappedBody.Length);
        Assert.False(char.IsHighSurrogate(mappedBody[^1]));
        Assert.Equal(MaxBodyBytes - 3, Encoding.UTF8.GetByteCount(mappedBody));
    }

    /// <summary>ASCII の本文が上限を超えるとき、上限のバイト数ちょうどへ切り詰める。</summary>
    [Fact]
    public async Task MapAsync_AsciiBodyOverLimit_TruncatesToLimit()
    {
        // Arrange
        var body = new string('x', MaxBodyBytes + 1);
        using var response = CreateResponse(body);

        // Act
        var output = await HttpResponseMapper.MapAsync(response, CancellationToken.None);

        // Assert
        var mappedBody = Assert.IsType<string>(output["body"]);
        Assert.Equal(body[..MaxBodyBytes], mappedBody);
    }

    /// <summary>UTF-8 で上限ちょうどの本文は切り詰めない。</summary>
    [Fact]
    public async Task MapAsync_BodyExactlyAtLimit_ReturnsBodyUnchanged()
    {
        // Arrange
        // 「あ」（3 バイト）× 349,525 + ASCII 1 文字 = 1,048,576 バイト。
        var body = new string('あ', MaxBodyBytes / 3) + "x";
        using var response = CreateResponse(body);

        // Act
        var output = await HttpResponseMapper.MapAsync(response, CancellationToken.None);

        // Assert
        Assert.Equal(MaxBodyBytes, Encoding.UTF8.GetByteCount(body));
        Assert.Equal(body, output["body"]);
    }

    private static HttpResponseMessage CreateResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
}
