using System.Buffers;
using System.Text.Unicode;

namespace Statevia.Reference.Http;

/// <summary>http.request action の HTTP レスポンスを出力辞書へ変換する。</summary>
/// <remarks>
/// 応答本文は UTF-8 に直したときのバイト数で <see cref="MaxBodyBytes"/>（1 MiB）以内に切り詰める。
/// 切り詰めは Unicode スカラー値の境界で行い、サロゲートペアや多バイト文字の途中では切らない。
/// </remarks>
internal static class HttpResponseMapper
{
    /// <summary>
    /// 出力へ入れる応答本文の上限（UTF-8 のバイト数）。リクエスト本文の上限（<c>HttpRequestBuilder</c>）とそろえて 1 MiB とする。
    /// </summary>
    internal const int MaxBodyBytes = 1_048_576;

    /// <summary>HTTP レスポンスを action 出力へ変換する。</summary>
    /// <param name="response">変換する HTTP レスポンス。</param>
    /// <param name="ct">本文の読み込みを取り消すトークン。</param>
    /// <returns>
    /// <c>statusCode</c>（数値）、<c>headers</c>（ヘッダ名の大文字小文字を区別しない辞書）、<c>body</c>（文字列）を持つ辞書。
    /// <c>body</c> は UTF-8 で <see cref="MaxBodyBytes"/> バイトを超えれば、文字の途中で切らずに上限以内へ切り詰める。
    /// </returns>
    public static async Task<Dictionary<string, object?>> MapAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var responseBody = TruncateToUtf8ByteLimit(
            await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false),
            MaxBodyBytes);

        var responseHeaders = response.Headers
            .Concat(response.Content.Headers)
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (object?)string.Join(", ", group.SelectMany(item => item.Value)),
                StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["headers"] = responseHeaders,
            ["body"] = responseBody,
        };
    }

    /// <summary>
    /// 文字列を UTF-8 にしたとき <paramref name="maxBytes"/> バイト以内に収まる最長の先頭部分を返す。
    /// </summary>
    /// <remarks>
    /// <see cref="Utf8.FromUtf16(ReadOnlySpan{char}, Span{byte}, out int, out int, bool, bool)"/> は
    /// 書き込み先が足りないとき完全なスカラー値の境界で止まるので、その読み取り文字数で切れば文字の途中で切れない。
    /// 対にならないサロゲートは置換文字（UTF-8 で 3 バイト）として数える。<see cref="System.Text.Encoding.UTF8"/> の数え方と同じ。
    /// </remarks>
    private static string TruncateToUtf8ByteLimit(string text, int maxBytes)
    {
        // UTF-16 の 1 コード単位は UTF-8 で高々 3 バイトなので、短い本文は数えずに返す。
        if (text.Length <= maxBytes / 3)
        {
            return text;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            var status = Utf8.FromUtf16(text, buffer.AsSpan(0, maxBytes), out var charsRead, out _);
            return status == OperationStatus.Done ? text : text[..charsRead];
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
