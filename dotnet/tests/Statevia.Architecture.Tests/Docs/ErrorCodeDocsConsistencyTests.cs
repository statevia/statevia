using System.Reflection;
using Statevia.Core.Application.Contracts;
using Xunit;

namespace Statevia.Architecture.Tests.Docs;

/// <summary>
/// HTTP のエラーコードが <c>docs/reference/error-codes.md</c> の一覧と一致することを検証する。
/// </summary>
/// <remarks>
/// 実装の正本は <see cref="ApiErrorCodes"/> の定数。文書の正本は <c>`error.code`</c> 列を持つ表すべて
/// （HTTP ステータスごとに表が分かれている）。
/// 定期実行の結果（<c>schedule_runs.error_code</c>）のコードは HTTP の応答に出ないので照合しない。
/// 文書に載せないことが正しいコードは、除外一覧の <c>errorCodes</c> に理由つきで書く。
/// </remarks>
public sealed class ErrorCodeDocsConsistencyTests
{
    /// <summary>エラーコードの文書のリポジトリ相対パス。</summary>
    private const string ErrorCodesDocumentPath = "docs/reference/error-codes.md";

    /// <summary>エラーコードの列の見出し（バッククォートを除いた形）。</summary>
    private const string ErrorCodeColumn = "error.code";

    private static readonly Lazy<IReadOnlySet<string>> ImplementedCodes = new(ReadImplementedCodes);

    private static readonly Lazy<IReadOnlyList<MarkdownTableCell>> DocumentedCodeCells = new(() =>
        MarkdownTableReader.ReadColumn(
            File.ReadAllText(RepositoryPaths.Resolve(ErrorCodesDocumentPath)),
            ErrorCodeColumn,
            ErrorCodesDocumentPath));

    private static readonly Lazy<IReadOnlySet<string>> ExcludedCodes =
        new(() => DocsConsistencyExclusions.Load().ValuesFor(DocsConsistencyExclusions.ErrorCodes));

    /// <summary><see cref="ApiErrorCodes"/> のコードは、すべて文書に載っている。</summary>
    [Fact]
    public void ImplementedCodes_ShouldBeDocumented()
    {
        // Arrange
        var documented = DocumentedCodeCells.Value.Select(cell => cell.Text).ToHashSet(StringComparer.Ordinal);

        // Act
        var undocumented = ImplementedCodes.Value
            .Where(code => !documented.Contains(code) && !ExcludedCodes.Value.Contains(code))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.NotEmpty(ImplementedCodes.Value);
        Assert.True(
            undocumented.Count == 0,
            $"{ErrorCodesDocumentPath} に無いエラーコードです。HTTP ステータスの表に行を足すか、載せない理由を "
            + $"{DocsConsistencyExclusions.RelativePath} の errorCodes に書いてください:\n" + string.Join("\n", undocumented));
    }

    /// <summary>文書のコードは、すべて <see cref="ApiErrorCodes"/> にあり、1 回だけ載っている。</summary>
    [Fact]
    public void DocumentedCodes_ShouldBeImplementedOnce()
    {
        // Arrange
        var cells = DocumentedCodeCells.Value;

        // Act
        var unimplemented = cells
            .Where(cell => !ImplementedCodes.Value.Contains(cell.Text))
            .Select(cell => $"{ErrorCodesDocumentPath}:{cell.Line}: {cell.Text}（実装に無い）");
        var duplicated = cells
            .GroupBy(cell => cell.Text, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{ErrorCodesDocumentPath}:{string.Join(",", group.Select(cell => cell.Line))}: {group.Key}（重複）");
        var problems = unimplemented.Concat(duplicated).ToList();

        // Assert
        Assert.True(
            problems.Count == 0,
            $"文書のエラーコードが {nameof(ApiErrorCodes)} と合いません。実装に無いコードは文書から消してください:\n" + string.Join("\n", problems));
    }

    /// <summary>除外一覧の errorCodes は、実装にあって文書に無いコードだけを指す（不要になった除外を残さない）。</summary>
    [Fact]
    public void Exclusions_ShouldPointToImplementedButUndocumentedCodes()
    {
        // Arrange
        var documented = DocumentedCodeCells.Value.Select(cell => cell.Text).ToHashSet(StringComparer.Ordinal);

        // Act
        var staleExclusions = ExcludedCodes.Value
            .Where(code => !ImplementedCodes.Value.Contains(code) || documented.Contains(code))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.True(
            staleExclusions.Count == 0,
            $"{DocsConsistencyExclusions.RelativePath} の errorCodes に、実装に無いか文書に載っているコードがあります。除外を消してください:\n"
            + string.Join("\n", staleExclusions));
    }

    /// <summary><see cref="ApiErrorCodes"/> の公開定数の値を集める。</summary>
    /// <returns>実装のエラーコードの集合。</returns>
    private static IReadOnlySet<string> ReadImplementedCodes() =>
        typeof(ApiErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);
}
