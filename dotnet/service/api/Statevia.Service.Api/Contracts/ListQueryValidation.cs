using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Statevia.Service.Api.Contracts;

/// <summary>一覧のクエリで共通の検証（並び順と開始日時の期間）。</summary>
/// <remarks>
/// <para>空と空白だけの値は省略と同じに扱う。それ以外で許可する値の外は 422（<c>field</c> は項目名）にする。黙って既定に戻さない。</para>
/// <para>開始日時は ISO 8601 で、<c>Z</c> かオフセット（<c>+09:00</c> など）を必須とする。タイムゾーンの無い値は解釈が分かれるため受けない。</para>
/// </remarks>
internal static partial class ListQueryValidation
{
    /// <summary>空と空白だけを <see langword="null"/> にし、前後の空白を除く。</summary>
    /// <param name="value">クエリの値。</param>
    /// <returns>正規化した値。</returns>
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary><c>sortBy</c> と <c>sortOrder</c> を検証する。大文字小文字を区別する。</summary>
    /// <param name="sortBy">クエリの <c>sortBy</c>。</param>
    /// <param name="allowedSortKeys">許可する <c>sortBy</c>。</param>
    /// <param name="sortOrder">クエリの <c>sortOrder</c>。</param>
    /// <param name="sortByMemberName">エラーに載せる <c>sortBy</c> のメンバー名。</param>
    /// <param name="sortOrderMemberName">エラーに載せる <c>sortOrder</c> のメンバー名。</param>
    /// <returns>検証エラー。無ければ空。</returns>
    public static IEnumerable<ValidationResult> ValidateSort(
        string? sortBy,
        IReadOnlyCollection<string> allowedSortKeys,
        string? sortOrder,
        string sortByMemberName,
        string sortOrderMemberName)
    {
        if (Normalize(sortBy) is { } normalizedSortBy && !allowedSortKeys.Contains(normalizedSortBy, StringComparer.Ordinal))
        {
            yield return new ValidationResult(
                $"sortBy must be one of {string.Join(", ", allowedSortKeys)}",
                [sortByMemberName]);
        }

        if (Normalize(sortOrder) is { } normalizedSortOrder
            && !ListSortOrders.All.Contains(normalizedSortOrder, StringComparer.Ordinal))
        {
            yield return new ValidationResult(
                $"sortOrder must be one of {string.Join(", ", ListSortOrders.All)}",
                [sortOrderMemberName]);
        }
    }

    /// <summary>開始日時の期間（<c>startedFrom</c> 含む、<c>startedTo</c> 含まない）を検証する。</summary>
    /// <param name="startedFrom">クエリの <c>startedFrom</c>。</param>
    /// <param name="startedTo">クエリの <c>startedTo</c>。</param>
    /// <param name="startedFromMemberName">エラーに載せる <c>startedFrom</c> のメンバー名。</param>
    /// <param name="startedToMemberName">エラーに載せる <c>startedTo</c> のメンバー名。</param>
    /// <returns>検証エラー。無ければ空。</returns>
    public static IEnumerable<ValidationResult> ValidateStartedRange(
        string? startedFrom,
        string? startedTo,
        string startedFromMemberName,
        string startedToMemberName)
    {
        var fromValid = TryParseUtc(startedFrom, out var fromUtc);
        var toValid = TryParseUtc(startedTo, out var toUtc);
        if (!fromValid)
            yield return new ValidationResult(DateTimeFormatMessage("startedFrom"), [startedFromMemberName]);
        if (!toValid)
            yield return new ValidationResult(DateTimeFormatMessage("startedTo"), [startedToMemberName]);
        if (fromUtc is { } from && toUtc is { } to && from >= to)
            yield return new ValidationResult("startedFrom must be earlier than startedTo", [startedFromMemberName]);
    }

    /// <summary>開始日時のクエリを UTC に直す。検証を通った後に呼ぶ。</summary>
    /// <param name="value">クエリの値。</param>
    /// <returns>UTC の日時。省略時は <see langword="null"/>。</returns>
    /// <exception cref="InvalidOperationException">検証を通っていない値のとき。</exception>
    public static DateTime? ResolveUtc(string? value) =>
        TryParseUtc(value, out var utc)
            ? utc
            : throw new InvalidOperationException("Started range must be validated before resolving.");

    private static bool TryParseUtc(string? value, out DateTime? utc)
    {
        utc = null;
        if (Normalize(value) is not { } normalized)
            return true;

        if (!IsoDateTimeWithZone().IsMatch(normalized)
            || !DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        utc = parsed.UtcDateTime;
        return true;
    }

    private static string DateTimeFormatMessage(string fieldName) =>
        $"{fieldName} must be an ISO 8601 date-time with Z or an offset (e.g. 2026-11-14T00:00:00Z)";

    /// <summary>ISO 8601 の日付と時刻で、末尾が <c>Z</c> かオフセットの値。</summary>
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex IsoDateTimeWithZone();
}
