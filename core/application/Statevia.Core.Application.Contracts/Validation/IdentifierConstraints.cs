using System.Text.RegularExpressions;

namespace Statevia.Core.Application.Contracts.Validation;

/// <summary>キー・参照用識別子の ASCII allowlist。</summary>
/// <remarks>
/// <para>先頭は英字。以降は英数字と <c>.</c> <c>_</c> <c>-</c>。空白・記号・CJK は不可。</para>
/// <para>Engine の <c>IdentifierCharset.AllowedPattern</c> と同一文字列であること。</para>
/// </remarks>
public static class IdentifierConstraints
{
    /// <summary>定義名の最大長。Studio 現行パターンと一致させるため 100。</summary>
    public const int DefinitionNameMaxLength = 100;

    /// <summary>イベント名・resumeKey の最大長。Studio 現行イベント名と一致させるため 64。</summary>
    public const int EventNameMaxLength = 64;

    /// <summary>topic / 非空 key / HTTP initialState の最大長。ingress 現行 StringLength と一致させるため 256。</summary>
    public const int TopicMaxLength = 256;

    /// <summary>許可文字の正規表現。先頭は英字。</summary>
    public const string AllowedPattern = "^[A-Za-z][A-Za-z0-9._-]*$";

    /// <summary>JSON Path 未引用セグメント。先頭は英字またはアンダースコア。ハイフン・ドットは不可。</summary>
    public const string PathIdentifierPattern = "^[A-Za-z_][A-Za-z0-9_]*$";

    /// <summary>形式違反時のメッセージ。</summary>
    public const string FormatErrorMessage =
        "must be a letter followed by letters, digits, dots, underscores, or hyphens.";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>長さと文字種を満たすか。</summary>
    /// <param name="value">未 trim の識別子。</param>
    /// <param name="maxLength">項目ごとの最大長。</param>
    /// <returns>有効なら true。</returns>
    public static bool IsValid(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        if (value.Length is 0 || value.Length > maxLength)
            return false;

        return Regex.IsMatch(
            value,
            AllowedPattern,
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            MatchTimeout);
    }
}
