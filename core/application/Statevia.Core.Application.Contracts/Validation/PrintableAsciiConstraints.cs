using System.Text.RegularExpressions;

namespace Statevia.Core.Application.Contracts.Validation;

/// <summary>印字可能 ASCII（制御文字なし）。<c>X-Idempotency-Key</c> 向け。</summary>
/// <remarks>最大長は現行どおり置かない（DB 列は text）。ヘッダ未指定は検証しない。</remarks>
public static class PrintableAsciiConstraints
{
    /// <summary>スペースからチルダまでの印字可能 ASCII。</summary>
    public const string AllowedPattern = @"^[\x20-\x7E]+$";

    /// <summary>形式違反時のメッセージ。</summary>
    public const string FormatErrorMessage = "must be printable ASCII with no control characters.";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>空でなく印字可能 ASCII だけか。</summary>
    /// <param name="value">ヘッダ値。</param>
    /// <returns>有効なら true。</returns>
    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
            return false;

        return Regex.IsMatch(
            value,
            AllowedPattern,
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            MatchTimeout);
    }
}
