using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Statevia.Architecture.Tests.Docs;

/// <summary>
/// Service API のエンドポイントが <c>docs/specifications/api-http.md</c> の一覧と一致することを検証する。
/// </summary>
/// <remarks>
/// 実装の正本はコミット済みの OpenAPI（<c>dotnet/service/api/openapi/core-api-v1.openapi.json</c>）。
/// OpenAPI がコントローラと一致していることは Service API のテスト（<c>OpenApiDocumentTests</c>）が確かめる。
/// 文書の正本は §1 の表。§1 にパスが <c>/*</c> で終わる行（例: <c>* /v1/admin/*</c>）があるときは、
/// その行を個別の操作とは数えず、<see cref="DelegatedSections"/> に登録した節の表で 1 本ずつ照合する。
/// パスのテンプレート変数は名前を無視して比べる（<c>{executionId}</c> と <c>{id}</c> は同じ）。
/// 文書に載せないことが正しい操作は、除外一覧の <c>apiRoutes</c> に理由つきで書く。
/// </remarks>
public sealed partial class ApiRouteDocsConsistencyTests
{
    /// <summary>API の文書のリポジトリ相対パス。</summary>
    private const string ApiDocumentPath = "docs/specifications/api-http.md";

    /// <summary>コミット済みの OpenAPI のリポジトリ相対パス。</summary>
    private const string OpenApiPath = "dotnet/service/api/openapi/core-api-v1.openapi.json";

    /// <summary>エンドポイント一覧の節の見出し。</summary>
    private const string EndpointListHeading = "## 1. エンドポイント一覧";

    /// <summary>OpenAPI の Path Item のうち、HTTP メソッドを表すキー。</summary>
    private static readonly HashSet<string> HttpMethods = ["get", "post", "put", "patch", "delete", "head", "options"];

    /// <summary>
    /// §1 の <c>/*</c> の行が指す節。キーは §1 に書いたパス、値はその節の見出しと、節の表のパスに付ける接頭辞。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Heading, string Prefix)> DelegatedSections =
        new Dictionary<string, (string Heading, string Prefix)>(StringComparer.Ordinal)
        {
            ["/v1/admin/*"] = ("### 4.1.3 テナント管理者 API（初版）", "/v1/admin"),
        };

    private static readonly Lazy<IReadOnlySet<string>> ImplementedOperations = new(ReadImplementedOperations);

    private static readonly Lazy<IReadOnlySet<string>> DocumentedOperations = new(ReadDocumentedOperations);

    private static readonly Lazy<IReadOnlySet<string>> ExcludedOperations =
        new(() => DocsConsistencyExclusions.Load().ValuesFor(DocsConsistencyExclusions.ApiRoutes));

    /// <summary>実装にあるエンドポイントは、すべて <c>api-http.md</c> に載っている。</summary>
    [Fact]
    public void ImplementedOperations_ShouldBeDocumented()
    {
        // Arrange
        var implemented = ImplementedOperations.Value;
        var documented = DocumentedOperations.Value;

        // Act
        var undocumented = implemented
            .Where(operation => !documented.Contains(operation) && !ExcludedOperations.Value.Contains(operation))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.NotEmpty(implemented);
        Assert.True(
            undocumented.Count == 0,
            $"{ApiDocumentPath} の §1（または §1 が指す節）に無いエンドポイントです。表に行を足すか、載せない理由を "
            + $"{DocsConsistencyExclusions.RelativePath} の apiRoutes に書いてください:\n" + string.Join("\n", undocumented));
    }

    /// <summary><c>api-http.md</c> に載っているエンドポイントは、すべて実装にある。</summary>
    [Fact]
    public void DocumentedOperations_ShouldBeImplemented()
    {
        // Arrange
        var implemented = ImplementedOperations.Value;
        var documented = DocumentedOperations.Value;

        // Act
        var unimplemented = documented
            .Where(operation => !implemented.Contains(operation))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.True(
            unimplemented.Count == 0,
            $"{ApiDocumentPath} に載っているが実装（{OpenApiPath}）に無いエンドポイントです。"
            + "文書から消すか、実装を足したなら OpenAPI を書き出し直してください:\n" + string.Join("\n", unimplemented));
    }

    /// <summary>除外一覧の apiRoutes は、実装にあって文書に無い操作だけを指す（不要になった除外を残さない）。</summary>
    [Fact]
    public void Exclusions_ShouldPointToImplementedButUndocumentedOperations()
    {
        // Arrange
        var implemented = ImplementedOperations.Value;
        var documented = DocumentedOperations.Value;

        // Act
        var staleExclusions = ExcludedOperations.Value
            .Where(operation => !implemented.Contains(operation) || documented.Contains(operation))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.True(
            staleExclusions.Count == 0,
            $"{DocsConsistencyExclusions.RelativePath} の apiRoutes に、実装に無いか文書に載っている操作があります。除外を消してください:\n"
            + string.Join("\n", staleExclusions));
    }

    /// <summary>コミット済みの OpenAPI から <c>METHOD /path</c> の集合を読む。</summary>
    /// <returns>実装の操作の集合。</returns>
    private static IReadOnlySet<string> ReadImplementedOperations()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryPaths.Resolve(OpenApiPath)));
        return document.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value
                .EnumerateObject()
                .Where(operation => HttpMethods.Contains(operation.Name))
                .Select(operation => FormatOperation(operation.Name, path.Name)))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// <c>api-http.md</c> の §1 と、§1 の <c>/*</c> の行が指す節から <c>METHOD /path</c> の集合を読む。
    /// </summary>
    /// <returns>文書の操作の集合。</returns>
    /// <exception cref="MarkdownTableFormatException">表が読めない、または <c>/*</c> の行の行き先が登録されていないとき。</exception>
    private static IReadOnlySet<string> ReadDocumentedOperations()
    {
        var markdown = File.ReadAllText(RepositoryPaths.Resolve(ApiDocumentPath));
        var endpointRows = ReadMethodAndPathRows(markdown, EndpointListHeading);

        var unknownDelegations = endpointRows
            .Where(row => row.Path.EndsWith("/*", StringComparison.Ordinal) && !DelegatedSections.ContainsKey(row.Path))
            .Select(row => $"{ApiDocumentPath}:{row.Line}: {row.Path}")
            .ToList();
        if (unknownDelegations.Count > 0)
        {
            throw new MarkdownTableFormatException(
                "§1 の /* の行が指す節が照合テストに登録されていません。行を個別の操作に展開するか、"
                + $"{nameof(ApiRouteDocsConsistencyTests)}.{nameof(DelegatedSections)} に節を足してください:\n"
                + string.Join("\n", unknownDelegations));
        }

        var directOperations = endpointRows
            .Where(row => !row.Path.EndsWith("/*", StringComparison.Ordinal))
            .Select(row => FormatOperation(row.Method, row.Path));
        var delegatedOperations = endpointRows
            .Where(row => DelegatedSections.ContainsKey(row.Path))
            .SelectMany(row =>
            {
                var (heading, prefix) = DelegatedSections[row.Path];
                return ReadMethodAndPathRows(markdown, heading).Select(sectionRow => FormatOperation(sectionRow.Method, prefix + sectionRow.Path));
            });
        return directOperations.Concat(delegatedOperations).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>節の表から「メソッド」と「パス」の列を行ごとに組にして読む。</summary>
    /// <param name="markdown">文書の全文。</param>
    /// <param name="sectionHeading">表がある節の見出し。</param>
    /// <returns>メソッド・パス・行番号の組。</returns>
    /// <exception cref="MarkdownTableFormatException">表が読めないとき、または 2 つの列の行数が合わないとき。</exception>
    private static IReadOnlyList<(string Method, string Path, int Line)> ReadMethodAndPathRows(string markdown, string sectionHeading)
    {
        var methods = MarkdownTableReader.ReadColumn(markdown, "メソッド", ApiDocumentPath, sectionHeading);
        var paths = MarkdownTableReader.ReadColumn(markdown, "パス", ApiDocumentPath, sectionHeading);
        return methods.Count == paths.Count && methods.Zip(paths).All(pair => pair.First.Line == pair.Second.Line)
            ? methods.Zip(paths, (method, path) => (method.Text, path.Text, method.Line)).ToList()
            : throw new MarkdownTableFormatException(
                $"{ApiDocumentPath}: 見出し「{sectionHeading}」の節で「メソッド」と「パス」の列を同じ表から読めません。");
    }

    /// <summary>メソッドとパスを照合用の <c>METHOD /path</c> にそろえる。テンプレート変数は名前を消して <c>{}</c> にする。</summary>
    /// <param name="method">HTTP メソッド（大文字小文字は問わない）。</param>
    /// <param name="path">パス。</param>
    /// <returns>照合用の文字列。</returns>
    private static string FormatOperation(string method, string path) =>
        $"{method.Trim().ToUpperInvariant()} {TemplateVariableRegex().Replace(path.Trim(), "{}")}";

    /// <summary>パスのテンプレート変数（<c>{executionId}</c> など）。</summary>
    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex TemplateVariableRegex();
}
