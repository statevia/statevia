using System.Reflection;
using Statevia.Core.Application.Contracts.Security;
using Xunit;

namespace Statevia.Architecture.Tests.Docs;

/// <summary>
/// 権限カタログが <c>docs/reference/permission-keys.md</c> の「カタログ」の表と一致することを検証する。
/// </summary>
/// <remarks>
/// 実装の正本は <see cref="PermissionCatalog.Entries"/>（DB の <c>permission_definitions</c> へ seed される）。
/// 文書は <see cref="WellKnownPermissionKeys"/> も正本実装として挙げているので、その定数がすべてカタログにあることも確かめる。
/// 表の 3 列（Permission key・表示ラベル・i18n キー）を照合する。
/// 文書に載せないことが正しいキーは、除外一覧の <c>permissionKeys</c> に理由つきで書く。
/// </remarks>
public sealed class PermissionKeyDocsConsistencyTests
{
    /// <summary>権限キーの文書のリポジトリ相対パス。</summary>
    private const string PermissionKeysDocumentPath = "docs/reference/permission-keys.md";

    /// <summary>カタログの表がある節の見出し。</summary>
    private const string CatalogHeading = "## カタログ（初版）";

    private static readonly Lazy<IReadOnlyDictionary<string, DocumentedPermission>> DocumentedPermissions = new(ReadDocumentedPermissions);

    private static readonly Lazy<IReadOnlySet<string>> ExcludedKeys =
        new(() => DocsConsistencyExclusions.Load().ValuesFor(DocsConsistencyExclusions.PermissionKeys));

    /// <summary>カタログのキーは、すべて文書の表に載っている。</summary>
    [Fact]
    public void CatalogKeys_ShouldBeDocumented()
    {
        // Arrange
        var documented = DocumentedPermissions.Value;

        // Act
        var undocumented = PermissionCatalog.Entries
            .Select(entry => entry.PermissionKey)
            .Where(key => !documented.ContainsKey(key) && !ExcludedKeys.Value.Contains(key))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.NotEmpty(PermissionCatalog.Entries);
        Assert.True(
            undocumented.Count == 0,
            $"{PermissionKeysDocumentPath} の「カタログ」の表に無い権限キーです。表に行を足すか、載せない理由を "
            + $"{DocsConsistencyExclusions.RelativePath} の permissionKeys に書いてください:\n" + string.Join("\n", undocumented));
    }

    /// <summary>文書の表のキーは、すべてカタログにある。</summary>
    [Fact]
    public void DocumentedKeys_ShouldBeInCatalog()
    {
        // Arrange
        var catalogKeys = PermissionCatalog.Entries.Select(entry => entry.PermissionKey).ToHashSet(StringComparer.Ordinal);

        // Act
        var unknown = DocumentedPermissions.Value.Values
            .Where(permission => !catalogKeys.Contains(permission.Key))
            .Select(permission => $"{PermissionKeysDocumentPath}:{permission.Line}: {permission.Key}")
            .ToList();

        // Assert
        Assert.True(unknown.Count == 0, "文書に載っているがカタログ（PermissionCatalog.Entries）に無い権限キーです:\n" + string.Join("\n", unknown));
    }

    /// <summary>両方にあるキーは、表示ラベルと i18n キーも一致する。</summary>
    [Fact]
    public void DocumentedLabels_ShouldMatchCatalog()
    {
        // Arrange
        var documented = DocumentedPermissions.Value;

        // Act
        var mismatches = PermissionCatalog.Entries
            .Where(entry => documented.ContainsKey(entry.PermissionKey))
            .Select(entry => (Entry: entry, Documented: documented[entry.PermissionKey]))
            .Where(pair => pair.Documented.DisplayLabel != pair.Entry.DisplayLabel || pair.Documented.DisplayKey != (pair.Entry.DisplayKey ?? string.Empty))
            .Select(pair =>
                $"{PermissionKeysDocumentPath}:{pair.Documented.Line}: {pair.Entry.PermissionKey} の表示ラベル・i18n キーは、"
                + $"文書「{pair.Documented.DisplayLabel}」「{pair.Documented.DisplayKey}」、実装「{pair.Entry.DisplayLabel}」「{pair.Entry.DisplayKey}」")
            .ToList();

        // Assert
        Assert.True(mismatches.Count == 0, "表示ラベルか i18n キーが実装と違います:\n" + string.Join("\n", mismatches));
    }

    /// <summary><see cref="WellKnownPermissionKeys"/> の定数は、すべてカタログにある。</summary>
    [Fact]
    public void WellKnownPermissionKeys_ShouldAllBeInCatalog()
    {
        // Arrange
        var constants = typeof(WellKnownPermissionKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (Name: field.Name, Value: (string)field.GetRawConstantValue()!))
            .ToList();
        var catalogKeys = PermissionCatalog.Entries.Select(entry => entry.PermissionKey).ToHashSet(StringComparer.Ordinal);

        // Act
        var missing = constants
            .Where(constant => !catalogKeys.Contains(constant.Value))
            .Select(constant => $"{nameof(WellKnownPermissionKeys)}.{constant.Name} = {constant.Value}")
            .ToList();

        // Assert
        Assert.NotEmpty(constants);
        Assert.True(missing.Count == 0, "PermissionCatalog.Entries に無い定数です:\n" + string.Join("\n", missing));
    }

    /// <summary>除外一覧の permissionKeys は、カタログにあって文書に無いキーだけを指す（不要になった除外を残さない）。</summary>
    [Fact]
    public void Exclusions_ShouldPointToCatalogKeysMissingFromDocument()
    {
        // Arrange
        var catalogKeys = PermissionCatalog.Entries.Select(entry => entry.PermissionKey).ToHashSet(StringComparer.Ordinal);
        var documented = DocumentedPermissions.Value;

        // Act
        var staleExclusions = ExcludedKeys.Value
            .Where(key => !catalogKeys.Contains(key) || documented.ContainsKey(key))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.True(
            staleExclusions.Count == 0,
            $"{DocsConsistencyExclusions.RelativePath} の permissionKeys に、カタログに無いか文書に載っているキーがあります。除外を消してください:\n"
            + string.Join("\n", staleExclusions));
    }

    /// <summary>文書の「カタログ」の表を読み、キーごとの表示ラベル・i18n キー・行番号を返す。</summary>
    /// <returns>キーをキーにした文書の行。</returns>
    /// <exception cref="MarkdownTableFormatException">表が読めない、3 列が同じ行から読めない、またはキーが重複しているとき。</exception>
    private static IReadOnlyDictionary<string, DocumentedPermission> ReadDocumentedPermissions()
    {
        var markdown = File.ReadAllText(RepositoryPaths.Resolve(PermissionKeysDocumentPath));
        var keys = MarkdownTableReader.ReadColumn(markdown, "Permission key", PermissionKeysDocumentPath, CatalogHeading);
        var labels = MarkdownTableReader.ReadColumn(markdown, "表示ラベル（参考）", PermissionKeysDocumentPath, CatalogHeading);
        var displayKeys = MarkdownTableReader.ReadColumn(markdown, "i18n キー（参考）", PermissionKeysDocumentPath, CatalogHeading);
        if (keys.Count != labels.Count || keys.Count != displayKeys.Count || keys.Select(cell => cell.Line).Except(labels.Select(cell => cell.Line)).Any())
        {
            throw new MarkdownTableFormatException($"{PermissionKeysDocumentPath}: 「カタログ」の表の 3 列を同じ行から読めません。");
        }

        var permissions = keys
            .Select((key, index) => new DocumentedPermission(key.Text, labels[index].Text, displayKeys[index].Text, key.Line))
            .ToList();
        var duplicates = permissions.GroupBy(permission => permission.Key).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        return duplicates.Count == 0
            ? permissions.ToDictionary(permission => permission.Key, StringComparer.Ordinal)
            : throw new MarkdownTableFormatException($"{PermissionKeysDocumentPath}: 「カタログ」の表でキーが重複しています: {string.Join(", ", duplicates)}");
    }

    /// <summary>文書の「カタログ」の表の 1 行。</summary>
    /// <param name="Key">Permission key。</param>
    /// <param name="DisplayLabel">表示ラベル。</param>
    /// <param name="DisplayKey">i18n キー。空なら i18n キーなし。</param>
    /// <param name="Line">行番号（1 始まり）。</param>
    private sealed record DocumentedPermission(string Key, string DisplayLabel, string DisplayKey, int Line);
}
