using System.Text.Json;

namespace Statevia.Core.Engine.Engine;

/// <summary>チェックポイント向け JSON 変換ヘルパー。</summary>
internal static class CheckpointJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>任意オブジェクトを JsonElement に変換する（null は null）。</summary>
    /// <param name="value">変換する値。null なら null を返す。</param>
    /// <returns>JSON 要素。<paramref name="value"/> が null なら <see langword="null"/>。</returns>
    public static JsonElement? ToElement(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.Clone();
        }

        return JsonSerializer.SerializeToElement(value, Options);
    }

    /// <summary>JsonElement を object? に戻す（オブジェクト/配列は Dictionary/List）。</summary>
    /// <param name="element">変換する JSON 要素。</param>
    /// <returns>変換した値。<paramref name="element"/> が null なら <see langword="null"/>。</returns>
    public static object? FromElement(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        return FromElement(element.Value);
    }

    /// <summary>JsonElement を object? に戻す。</summary>
    /// <param name="element">変換する JSON 要素。</param>
    /// <returns>変換した値。オブジェクトは Dictionary、配列は List にする。</returns>
    public static object? FromElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt64(out var l)
                ? l
                : element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(FromElement)
                .ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    p => p.Name,
                    p => FromElement(p.Value),
                    StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
}
