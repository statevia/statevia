using System.Text.RegularExpressions;
using Xunit;

namespace Statevia.Architecture.Tests;

/// <summary>
/// <c>[LoggerMessage]</c> の EventId がリポジトリ全体で採番規則に従うことを検証する。
/// </summary>
/// <remarks>
/// 規則の正本は <c>docs/reference/logging-property-keys.md</c> の「EventId」節。
/// ActionHost や Runtime など本プロジェクトが参照しないアセンブリも対象にするため、
/// リフレクションではなくリポジトリ内の <c>*.cs</c> を直接走査する。
/// </remarks>
public sealed partial class LoggerMessageEventIdTests
{
    /// <summary>EventId の下限（5桁の最小値）。</summary>
    private const int MinimumEventId = 10000;

    /// <summary>EventId の上限（6桁にしない規則による5桁の最大値）。</summary>
    private const int MaximumEventId = 99999;

    /// <summary>採番規則を記した docs のリポジトリ相対パス。</summary>
    private static readonly string LoggingPropertyKeysDocPath =
        Path.Combine("docs", "reference", "logging-property-keys.md");

    /// <summary>相対パスをディレクトリ単位に分ける区切り文字（OS 既定と代替の両方）。</summary>
    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private static readonly Lazy<IReadOnlyList<LoggerMessageEventId>> EventIds = new(ScanEventIds);

    /// <summary>リポジトリ全体で EventId の整数が重複しない。</summary>
    [Fact]
    public void EventIds_ShouldBeUniqueAcrossRepository()
    {
        // Arrange
        var eventIds = EventIds.Value;

        // Act
        var duplicates = eventIds
            .GroupBy(eventId => eventId.Id)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(eventId => eventId.Location))}")
            .ToList();

        // Assert
        Assert.NotEmpty(eventIds);
        Assert.True(duplicates.Count == 0, "EventId が重複しています:\n" + string.Join("\n", duplicates));
    }

    /// <summary>EventId は5桁で、連番（下2桁）に領域の代表 <c>00</c> を使わない。</summary>
    [Fact]
    public void EventIds_ShouldBeFiveDigitsWithNonZeroSequence()
    {
        // Arrange
        var eventIds = EventIds.Value;

        // Act
        var violations = eventIds
            .Where(eventId => eventId.Id is < MinimumEventId or > MaximumEventId || eventId.Id % 100 == 0)
            .Select(eventId => $"{eventId.Id}: {eventId.Location}")
            .ToList();

        // Assert
        Assert.True(violations.Count == 0, "EventId が5桁規則に反しています:\n" + string.Join("\n", violations));
    }

    /// <summary>EventId は docs の領域表に載った帯のいずれかに収まる。</summary>
    [Fact]
    public void EventIds_ShouldFallWithinDocumentedBands()
    {
        // Arrange
        var bands = ReadDocumentedBands();
        var eventIds = EventIds.Value;

        // Act
        var outsideBands = eventIds
            .Where(eventId => !bands.Any(band => eventId.Id >= band.Start && eventId.Id <= band.End))
            .Select(eventId => $"{eventId.Id}: {eventId.Location}")
            .ToList();

        // Assert
        Assert.NotEmpty(bands);
        Assert.True(outsideBands.Count == 0, $"{LoggingPropertyKeysDocPath} の領域表に無い EventId です:\n" + string.Join("\n", outsideBands));
    }

    /// <summary>
    /// リポジトリ内の <c>[LoggerMessage]</c> をすべて走査し、EventId と位置を返す。
    /// </summary>
    /// <returns>検出した EventId の一覧。</returns>
    /// <exception cref="Xunit.Sdk.XunitException">EventId を持たない <c>[LoggerMessage]</c> があったとき。</exception>
    private static IReadOnlyList<LoggerMessageEventId> ScanEventIds()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceFiles = Directory
            .EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsExcludedDirectory(Path.GetRelativePath(repositoryRoot, path)));

        var results = sourceFiles
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
                return LoggerMessageAttributeRegex().Matches(source).Select(match =>
                {
                    var line = source.AsSpan(0, match.Index).Count('\n') + 1;
                    var location = $"{relativePath}:{line}";
                    var eventIdMatch = EventIdArgumentRegex().Match(match.Groups["arguments"].Value);
                    Assert.True(eventIdMatch.Success, $"[LoggerMessage] に EventId がありません: {location}");
                    return new LoggerMessageEventId(int.Parse(eventIdMatch.Groups["id"].Value), location);
                });
            })
            .ToList();
        return results;
    }

    /// <summary>
    /// docs の領域表から <c>`開始`–`終了`</c> 形式の帯を読み取る。
    /// </summary>
    /// <returns>docs に記載された帯の一覧。</returns>
    private static IReadOnlyList<(int Start, int End)> ReadDocumentedBands()
    {
        var document = File.ReadAllText(Path.Combine(FindRepositoryRoot(), LoggingPropertyKeysDocPath));
        return DocumentedBandRegex()
            .Matches(document)
            .Select(match => (int.Parse(match.Groups["start"].Value), int.Parse(match.Groups["end"].Value)))
            .ToList();
    }

    /// <summary>
    /// テスト実行ディレクトリから親をたどり、<c>dotnet/statevia.sln</c> があるリポジトリルートを返す。
    /// </summary>
    /// <returns>リポジトリルートの絶対パス。</returns>
    /// <exception cref="DirectoryNotFoundException">ルートが見つからないとき。</exception>
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dotnet", "statevia.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("dotnet/statevia.sln を含むリポジトリルートが見つかりません。");
    }

    /// <summary>ビルド出力・依存パッケージ・worktree など、走査しないディレクトリかを判定する。</summary>
    /// <remarks>
    /// <c>.claude</c> を外すのは、Claude Code が worktree（リポジトリの完全な写し）を <c>.claude/worktrees/</c> に置くため。
    /// 写しを数えると、すべての EventId が本体との組で重複と報告される。<c>.claude</c> は設定の置き場で、C# のソースは無い。
    /// </remarks>
    /// <param name="relativePath">リポジトリルートからの相対パス。</param>
    private static bool IsExcludedDirectory(string relativePath) =>
        relativePath
            .Split(PathSeparators)
            .Any(segment => segment is "bin" or "obj" or "node_modules" or ".git" or "artifacts" or ".claude");

    /// <summary><c>LoggerMessage</c> 属性の引数部を捕捉する（複数行の属性も対象）。</summary>
    [GeneratedRegex(@"\[LoggerMessage\s*\((?<arguments>.*?)\)\s*\]", RegexOptions.Singleline)]
    private static partial Regex LoggerMessageAttributeRegex();

    /// <summary>属性引数のうち <c>EventId = 数値</c> を捕捉する。</summary>
    [GeneratedRegex(@"\bEventId\s*=\s*(?<id>\d+)")]
    private static partial Regex EventIdArgumentRegex();

    /// <summary>docs の表セル <c>`10001`–`10099`</c> を捕捉する。</summary>
    [GeneratedRegex(@"`(?<start>\d{5})`–`(?<end>\d{5})`")]
    private static partial Regex DocumentedBandRegex();

    /// <summary>検出した EventId と、そのソース上の位置。</summary>
    /// <param name="Id">EventId の整数。</param>
    /// <param name="Location">リポジトリ相対パスと行番号。</param>
    private sealed record LoggerMessageEventId(int Id, string Location);
}
