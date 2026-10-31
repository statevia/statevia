using System.Text.Json;

namespace Statevia.Tools.Capacity;

/// <summary>Service API のエラー JSON から code / message を取り出す。トークンは残さない。</summary>
internal static class CapacityApiErrorParser
{
    private const int MaxMessageLength = 180;

    /// <summary>応答本文を解釈する。JSON でなければ本文を切り詰めて message にする。</summary>
    /// <param name="body">HTTP 本文。空可。</param>
    /// <returns>エラーコードとサニタイズ済みメッセージ。</returns>
    public static (string? Code, string? Message) Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object)
            {
                return (ReadString(error, "code"), Sanitize(ReadString(error, "message")));
            }

            var title = ReadString(root, "title");
            var status = ReadStatusLabel(root);
            return (status, Sanitize(title ?? TruncateRaw(body)));
        }
        catch (JsonException)
        {
            return (null, Sanitize(body));
        }
    }

    private static string? ReadString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ReadStatusLabel(JsonElement root)
    {
        if (!root.TryGetProperty("status", out var status))
        {
            return null;
        }

        return status.ValueKind switch
        {
            JsonValueKind.Number when status.TryGetInt32(out var code) => code.ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonValueKind.String => status.GetString(),
            _ => null,
        };
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (LooksLikeSecret(value))
        {
            return "[redacted]";
        }

        var flattened = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return TruncateRaw(flattened);
    }

    private static string TruncateRaw(string value)
    {
        return value.Length <= MaxMessageLength ? value : value[..MaxMessageLength];
    }

    private static bool LooksLikeSecret(string value)
    {
        return value.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)
            || value.Contains("eyJ", StringComparison.Ordinal);
    }
}
