using System.Text;

namespace Statevia.Reference.Http;

/// <summary>http.request action の HTTP レスポンスを出力辞書へ変換する。</summary>
internal static class HttpResponseMapper
{
    private const int MaxBodyBytes = 1_048_576;

    /// <summary>HTTP レスポンスを action 出力へ変換する。</summary>
    /// <param name="response">変換する HTTP レスポンス。</param>
    /// <param name="ct">本文の読み取りのキャンセル。</param>
    /// <returns><c>statusCode</c>（数値）、<c>headers</c>（同名ヘッダは <c>, </c> で連結）、<c>body</c>（文字列。長すぎれば切り詰める）を持つ辞書。</returns>
    public static async Task<Dictionary<string, object?>> MapAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (Encoding.UTF8.GetByteCount(responseBody) > MaxBodyBytes)
        {
            responseBody = responseBody[..MaxBodyBytes];
        }

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
}
