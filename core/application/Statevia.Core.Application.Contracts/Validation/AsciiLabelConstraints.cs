using System.Text.RegularExpressions;

namespace Statevia.Core.Application.Contracts.Validation;

/// <summary>表示名の ASCII allowlist。</summary>
/// <remarks>
/// <para>先頭末尾は英数字。途中のみスペース・<c>.</c> <c>_</c> <c>-</c> を許可する（<c>Acme Corporation</c>）。</para>
/// <para>HTTP 境界では Trim してから <see cref="IsValid"/> する。</para>
/// </remarks>
public static class AsciiLabelConstraints
{
    /// <summary>グループ名・API キー名の既定最大長。現行 Admin DTO と一致させるため 128。</summary>
    public const int DefaultMaxLength = 128;

    /// <summary>ユーザー / テナント displayName の最大長。現行テナント作成と一致させるため 256。</summary>
    public const int DisplayNameMaxLength = 256;

    /// <summary>許可文字の正規表現。先頭末尾は英数字。</summary>
    public const string AllowedPattern = "^[A-Za-z0-9](?:[A-Za-z0-9 ._-]*[A-Za-z0-9])?$";

    /// <summary>形式違反時のメッセージ。</summary>
    public const string FormatErrorMessage =
        "must be ASCII letters or digits; space, hyphen, underscore, and dot are allowed only between them.";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>長さと文字種を満たすか。</summary>
    /// <param name="value">Trim 済みを想定した表示名。</param>
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
