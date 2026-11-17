using System.Text.Json;
using System.Text.Json.Serialization;

namespace Statevia.Architecture.Tests.Docs;

/// <summary>
/// 文書と実装の照合テストで、差分として扱わない項目の一覧。
/// </summary>
/// <remarks>
/// 正本は <c>dotnet/tests/Statevia.Architecture.Tests/Docs/docs-consistency-exclusions.json</c>。
/// 内部用で利用者向けの文書に載せないルートなど、文書に無いことが正しい項目だけを置く。
/// 照合の失敗を一時的に黙らせるためには使わない。各項目は理由（<c>reason</c>）が必須で、
/// 値か理由が空の項目、同じ領域での値の重複、未知の領域があれば読み込みを失敗させる。
/// </remarks>
/// <param name="Categories">領域ごとの除外項目。キーは <see cref="KnownCategories"/> のどれか。</param>
internal sealed record DocsConsistencyExclusions(IReadOnlyDictionary<string, IReadOnlyList<DocsConsistencyExclusion>> Categories)
{
    /// <summary>除外一覧のリポジトリ相対パス。</summary>
    public const string RelativePath = "dotnet/tests/Statevia.Architecture.Tests/Docs/docs-consistency-exclusions.json";

    /// <summary>API ルートの照合（OpenAPI と <c>api-http.md</c>）。値は <c>GET /v1/...</c> の形。</summary>
    public const string ApiRoutes = "apiRoutes";

    /// <summary>権限キーの照合（<c>WellKnownPermissionKeys</c> と <c>permission-keys.md</c>）。</summary>
    public const string PermissionKeys = "permissionKeys";

    /// <summary>エラーコードの照合（エラーコードの定数と <c>error-codes.md</c>）。</summary>
    public const string ErrorCodes = "errorCodes";

    /// <summary>画面の照合（Studio の <c>page.tsx</c> と <c>ui-user-guide.md</c>）。</summary>
    public const string UiRoutes = "uiRoutes";

    /// <summary>除外一覧に書ける領域。</summary>
    public static readonly IReadOnlyList<string> KnownCategories = [ApiRoutes, PermissionKeys, ErrorCodes, UiRoutes];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
    };

    /// <summary>リポジトリの除外一覧を読む。</summary>
    /// <returns>検証済みの除外一覧。</returns>
    /// <exception cref="InvalidDataException">形式が不正なとき（<see cref="Parse"/> を参照）。</exception>
    public static DocsConsistencyExclusions Load() =>
        Parse(File.ReadAllText(RepositoryPaths.Resolve(RelativePath)), RelativePath);

    /// <summary>除外一覧の JSON を読み、検証する。</summary>
    /// <param name="json">除外一覧の JSON。ルートは領域名をキーにしたオブジェクト。</param>
    /// <param name="sourceName">エラーメッセージに出す名前。</param>
    /// <returns>検証済みの除外一覧。書かれていない領域は空の一覧になる。</returns>
    /// <exception cref="InvalidDataException">
    /// JSON として読めない、未知の領域がある、値か理由が空の項目がある、または同じ領域で値が重複しているとき。
    /// </exception>
    public static DocsConsistencyExclusions Parse(string json, string sourceName)
    {
        Dictionary<string, List<ExclusionEntry?>>? document;
        try
        {
            document = JsonSerializer.Deserialize<Dictionary<string, List<ExclusionEntry?>>>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{sourceName}: JSON として読めません（{exception.Message}）。", exception);
        }

        var categories = document ?? [];
        var problems = categories.Keys
            .Where(category => !KnownCategories.Contains(category))
            .Select(category => $"未知の領域「{category}」があります。{string.Join(" / ", KnownCategories)} のどれかにしてください。")
            .Concat(categories.SelectMany(category => ValidateCategory(category.Key, category.Value)))
            .ToList();
        if (problems.Count > 0)
        {
            throw new InvalidDataException($"{sourceName}: 除外一覧が不正です。\n" + string.Join("\n", problems));
        }

        return new DocsConsistencyExclusions(KnownCategories.ToDictionary(
            category => category,
            IReadOnlyList<DocsConsistencyExclusion> (category) => categories.TryGetValue(category, out var entries)
                ? entries.Select(entry => new DocsConsistencyExclusion(entry!.Value!.Trim(), entry.Reason!.Trim())).ToList()
                : []));
    }

    /// <summary>領域の除外項目を返す。</summary>
    /// <param name="category">領域名（<see cref="KnownCategories"/> のどれか）。</param>
    /// <returns>除外項目。無ければ空。</returns>
    /// <exception cref="ArgumentOutOfRangeException">未知の領域を指定したとき。</exception>
    public IReadOnlyList<DocsConsistencyExclusion> For(string category) =>
        Categories.TryGetValue(category, out var exclusions)
            ? exclusions
            : throw new ArgumentOutOfRangeException(nameof(category), category, "未知の領域です。");

    /// <summary>領域の除外項目の値を集合で返す。照合の差分から引くときに使う。</summary>
    /// <param name="category">領域名（<see cref="KnownCategories"/> のどれか）。</param>
    /// <returns>除外する値の集合。</returns>
    public IReadOnlySet<string> ValuesFor(string category) =>
        For(category).Select(exclusion => exclusion.Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>1 つの領域の項目を検証し、問題を返す。</summary>
    /// <param name="category">領域名。</param>
    /// <param name="entries">JSON の項目。</param>
    /// <returns>問題のメッセージ。無ければ空。</returns>
    private static IEnumerable<string> ValidateCategory(string category, IReadOnlyList<ExclusionEntry?> entries)
    {
        var emptyProblems = entries
            .Select((entry, index) => (entry, index))
            .SelectMany(item => new[]
            {
                string.IsNullOrWhiteSpace(item.entry?.Value) ? $"{category}[{item.index}]: value が空です。" : null,
                string.IsNullOrWhiteSpace(item.entry?.Reason) ? $"{category}[{item.index}]: reason が空です。文書に載せない理由を書いてください。" : null,
            })
            .OfType<string>();
        var duplicateProblems = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry?.Value))
            .GroupBy(entry => entry!.Value!.Trim(), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{category}: value「{group.Key}」が重複しています。");
        return emptyProblems.Concat(duplicateProblems);
    }

    /// <summary>JSON の 1 項目。検証前なので null を許す。</summary>
    /// <param name="Value">除外する値。</param>
    /// <param name="Reason">文書に載せない理由。</param>
    private sealed record ExclusionEntry(
        [property: JsonPropertyName("value")] string? Value,
        [property: JsonPropertyName("reason")] string? Reason);
}

/// <summary>照合で差分として扱わない 1 項目。</summary>
/// <param name="Value">除外する値（ルート・キー・コード・画面のパス）。</param>
/// <param name="Reason">文書に載せない理由。</param>
internal sealed record DocsConsistencyExclusion(string Value, string Reason);
