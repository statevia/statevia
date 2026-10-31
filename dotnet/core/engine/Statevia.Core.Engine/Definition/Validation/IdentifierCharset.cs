using System.Text.RegularExpressions;

namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>定義 YAML 識別子の ASCII allowlist。</summary>
/// <remarks>
/// <para>Application.Contracts の <c>IdentifierConstraints</c> と同じパターン文字列を持つ。循環依存を避けるため Engine 側に複製する。</para>
/// <para>ドリフト禁止。API テストで文字列一致を断言する。</para>
/// </remarks>
public static class IdentifierCharset
{
    /// <summary>状態名・イベント名・module alias・action セグメント。Contracts の Identifier と同一。</summary>
    public const string AllowedPattern = "^[A-Za-z][A-Za-z0-9._-]*$";

    /// <summary>JSON Path 未引用セグメント。Contracts の PathIdentifier と同一。</summary>
    public const string PathIdentifierPattern = "^[A-Za-z_][A-Za-z0-9_]*$";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>空でなく Identifier パターンを満たすか。</summary>
    /// <param name="value">Trim 済みを想定した識別子。</param>
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

    /// <summary>未引用 Path セグメントとして有効か。</summary>
    /// <param name="value">セグメント文字列。</param>
    /// <returns>有効なら true。</returns>
    public static bool IsPathIdentifier(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
            return false;

        return Regex.IsMatch(
            value,
            PathIdentifierPattern,
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
            MatchTimeout);
    }
}
