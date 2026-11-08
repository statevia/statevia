using Statevia.Core.Actions.Abstractions.Execution;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Statevia.Reference.Http;

/// <summary>http.request action の HTTP リクエスト組み立て。</summary>
internal static class HttpRequestBuilder
{
    private const int MaxBodyBytes = 1_048_576;

    /// <summary>入力フィールドから <see cref="HttpRequestMessage"/> を構築する。</summary>
    /// <remarks>
    /// <para><c>headers</c> の文字列値をヘッダへ、<c>idempotencyKey</c> を <c>Idempotency-Key</c> ヘッダへ載せる。</para>
    /// <para><c>body</c> は文字列ならそのまま、オブジェクト・配列なら JSON 文字列にして <c>application/json</c> で送る。UTF-8 で 1 MiB を超えたら拒否する。</para>
    /// </remarks>
    /// <param name="fields">action の入力フィールド。<c>headers</c> / <c>idempotencyKey</c> / <c>body</c> を読む。</param>
    /// <param name="url">送信先 URL。検証は呼び出し側で済ませておく。</param>
    /// <param name="method">HTTP メソッド名。</param>
    /// <returns>組み立てたリクエスト。破棄は呼び出し側が持つ。</returns>
    /// <exception cref="ArgumentException"><c>body</c> が UTF-8 で 1 MiB を超えたとき。</exception>
    public static HttpRequestMessage Build(
        IReadOnlyDictionary<string, JsonElement> fields,
        string url,
        string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        ApplyHeaders(request, fields);
        ApplyIdempotencyKey(request, fields);
        ApplyBody(request, fields);
        return request;
    }

    private static void ApplyHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, JsonElement> fields)
    {
        if (!fields.TryGetValue("headers", out var headersElement)
            || headersElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var header in headersElement.EnumerateObject())
        {
            if (header.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Name, header.Value.GetString());
        }
    }

    private static void ApplyIdempotencyKey(HttpRequestMessage request, IReadOnlyDictionary<string, JsonElement> fields)
    {
        var idempotencyKey = ActionInputReader.OptionalString(fields, "idempotencyKey");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return;
        }

        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
    }

    private static void ApplyBody(HttpRequestMessage request, IReadOnlyDictionary<string, JsonElement> fields)
    {
        if (!fields.TryGetValue("body", out var bodyElement)
            || bodyElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array))
        {
            return;
        }

        var bodyText = bodyElement.ValueKind == JsonValueKind.String
            ? bodyElement.GetString() ?? string.Empty
            : bodyElement.GetRawText();
        if (Encoding.UTF8.GetByteCount(bodyText) > MaxBodyBytes)
        {
            throw new ArgumentException("http.request action body exceeds the maximum allowed size.");
        }

        request.Content = new StringContent(bodyText, Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    }
}
