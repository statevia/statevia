using System.Text.RegularExpressions;

namespace Statevia.Architecture.Tests.Docs;

/// <summary>
/// docs の Markdown の表から、指定した見出しの列のセルを読み出す。
/// </summary>
/// <remarks>
/// 文書と実装の照合テストが共有する。表の形が変わったときに「一致」と誤判定しないよう、
/// 見出しの列を持つ表が無い・列が足りない行がある・本体が空のときは <see cref="MarkdownTableFormatException"/> を投げる。
/// 対象はパイプ（<c>|</c>）区切りの GFM の表だけ。セル内の <c>\|</c> は区切りとみなさない。
/// コードブロックの中の表は読まない。
/// </remarks>
internal static partial class MarkdownTableReader
{
    /// <summary>
    /// 指定した見出しの列を持つ表をすべて読み、その列のセルを文書の順に返す。
    /// </summary>
    /// <param name="markdown">Markdown の全文。</param>
    /// <param name="columnHeader">列の見出し。前後の空白とバッククォートを除いて完全一致で比べる。</param>
    /// <param name="sourceName">エラーメッセージに出す文書名（リポジトリ相対パスなど）。</param>
    /// <param name="sectionHeading">
    /// 読む範囲を絞る見出しの行（例: <c>## 1. エンドポイント一覧</c>）。指定すると、その見出しから同じか上の階層の次の見出しまでだけを読む。
    /// 省略すると文書全体を読む。
    /// </param>
    /// <returns>列のセル。1 件以上。</returns>
    /// <exception cref="MarkdownTableFormatException">見出しの節が無い、列を持つ表が無い、列が足りない行がある、または本体が空のとき。</exception>
    public static IReadOnlyList<MarkdownTableCell> ReadColumn(
        string markdown,
        string columnHeader,
        string sourceName,
        string? sectionHeading = null)
    {
        var lines = markdown.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        var (start, end) = sectionHeading is null ? (0, lines.Length) : FindSection(lines, sectionHeading, sourceName);
        var insideCodeBlock = MarkCodeBlockLines(lines);

        var cells = new List<MarkdownTableCell>();
        var tableFound = false;
        var index = start;
        while (index < end)
        {
            if (insideCodeBlock[index] || !IsTableRow(lines[index]) || index + 1 >= end || !IsDelimiterRow(lines[index + 1]))
            {
                index++;
                continue;
            }

            var header = SplitRow(lines[index]).Select(StripCode).ToList();
            var columnIndex = header.IndexOf(columnHeader);
            var bodyStart = index + 2;
            var bodyEnd = bodyStart;
            while (bodyEnd < end && !insideCodeBlock[bodyEnd] && IsTableRow(lines[bodyEnd]))
            {
                bodyEnd++;
            }

            if (columnIndex >= 0)
            {
                tableFound = true;
                cells.AddRange(Enumerable.Range(bodyStart, bodyEnd - bodyStart).Select(rowIndex =>
                {
                    var row = SplitRow(lines[rowIndex]);
                    return columnIndex < row.Count
                        ? new MarkdownTableCell(StripCode(row[columnIndex]), row[columnIndex], rowIndex + 1)
                        : throw new MarkdownTableFormatException(
                            $"{sourceName}:{rowIndex + 1}: 表の行に「{columnHeader}」列（{columnIndex + 1} 列目）がありません。列の数を見出しに合わせてください。");
                }));
            }

            index = bodyEnd;
        }

        if (!tableFound)
        {
            throw new MarkdownTableFormatException(
                $"{sourceName}: 「{columnHeader}」列を持つ表が見つかりません{DescribeSection(sectionHeading)}。表の見出しが変わったなら、照合テストの列名も直してください。");
        }

        return cells.Count > 0
            ? cells
            : throw new MarkdownTableFormatException($"{sourceName}: 「{columnHeader}」列の表に行がありません{DescribeSection(sectionHeading)}。");
    }

    /// <summary>見出しの行から、同じか上の階層の次の見出しまでの範囲を返す。</summary>
    /// <param name="lines">文書の行。</param>
    /// <param name="sectionHeading">探す見出しの行。</param>
    /// <param name="sourceName">エラーメッセージに出す文書名。</param>
    /// <returns>見出しの次の行から節の終わりまでの範囲（終わりは含まない）。</returns>
    /// <exception cref="MarkdownTableFormatException">見出しが無いとき。</exception>
    private static (int Start, int End) FindSection(string[] lines, string sectionHeading, string sourceName)
    {
        var headingIndex = Array.FindIndex(lines, line => line.Trim() == sectionHeading.Trim());
        if (headingIndex < 0)
        {
            throw new MarkdownTableFormatException($"{sourceName}: 見出し「{sectionHeading}」が見つかりません。見出しが変わったなら、照合テストの見出しも直してください。");
        }

        var level = HeadingLevel(lines[headingIndex]);
        var nextIndex = Array.FindIndex(lines, headingIndex + 1, line => HeadingLevel(line) is > 0 and var nextLevel && nextLevel <= level);
        return (headingIndex + 1, nextIndex < 0 ? lines.Length : nextIndex);
    }

    /// <summary>行がコードブロック（<c>```</c> の囲み）の中かを行ごとに返す。囲みの行自体も中とみなす。</summary>
    /// <param name="lines">文書の行。</param>
    /// <returns>行ごとの判定。</returns>
    private static bool[] MarkCodeBlockLines(string[] lines)
    {
        var inside = false;
        return lines
            .Select(line =>
            {
                var isFence = line.TrimStart().StartsWith("```", StringComparison.Ordinal);
                var result = inside || isFence;
                inside ^= isFence;
                return result;
            })
            .ToArray();
    }

    /// <summary>ATX 見出し（<c>#</c> 〜 <c>######</c>）の階層を返す。見出しでなければ 0。</summary>
    /// <param name="line">文書の 1 行。</param>
    private static int HeadingLevel(string line)
    {
        var match = HeadingRegex().Match(line);
        return match.Success ? match.Groups["marks"].Length : 0;
    }

    /// <summary>表の行（<c>|</c> で始まる行）かを返す。</summary>
    /// <param name="line">文書の 1 行。</param>
    private static bool IsTableRow(string line) => line.TrimStart().StartsWith('|');

    /// <summary>見出しと本体を分ける区切りの行（<c>| --- | :---: |</c> など）かを返す。</summary>
    /// <param name="line">文書の 1 行。</param>
    private static bool IsDelimiterRow(string line) =>
        IsTableRow(line) && SplitRow(line).All(cell => DelimiterCellRegex().IsMatch(cell));

    /// <summary>表の行をセルに分ける。前後の空白を除き、<c>\|</c> は <c>|</c> に戻す。</summary>
    /// <param name="line">表の行。</param>
    /// <returns>セルの文字列。</returns>
    private static List<string> SplitRow(string line)
    {
        var trimmed = line.Trim();
        var inner = trimmed.Length >= 2 && trimmed.EndsWith('|') ? trimmed[1..^1] : trimmed[1..];
        return UnescapedPipeRegex().Split(inner).Select(cell => cell.Trim().Replace(@"\|", "|", StringComparison.Ordinal)).ToList();
    }

    /// <summary>セルからインラインコードのバッククォートを除き、前後の空白を除く。</summary>
    /// <param name="cell">セルの文字列。</param>
    private static string StripCode(string cell) => cell.Replace("`", string.Empty, StringComparison.Ordinal).Trim();

    /// <summary>エラーメッセージ用に、読んだ範囲を書き添える。</summary>
    /// <param name="sectionHeading">読む範囲を絞った見出し。無ければ空文字。</param>
    private static string DescribeSection(string? sectionHeading) =>
        sectionHeading is null ? string.Empty : $"（見出し「{sectionHeading}」の節）";

    /// <summary>ATX 見出しの <c>#</c> の並びを捕捉する。</summary>
    [GeneratedRegex(@"^(?<marks>#{1,6})\s")]
    private static partial Regex HeadingRegex();

    /// <summary>区切りの行のセル（<c>---</c>、<c>:---</c>、<c>---:</c>、<c>:---:</c>）。</summary>
    [GeneratedRegex(@"^:?-{3,}:?$")]
    private static partial Regex DelimiterCellRegex();

    /// <summary>バックスラッシュで逃がしていない <c>|</c>。</summary>
    [GeneratedRegex(@"(?<!\\)\|")]
    private static partial Regex UnescapedPipeRegex();
}

/// <summary>表から読んだ 1 つのセル。</summary>
/// <param name="Text">バッククォートを除いたセルの文字列。照合にはこれを使う。</param>
/// <param name="Raw">文書に書かれたままのセルの文字列（前後の空白は除く）。</param>
/// <param name="Line">セルがある行（1 始まり）。</param>
internal sealed record MarkdownTableCell(string Text, string Raw, int Line);

/// <summary>
/// docs の表を照合テストが読めないときに投げる例外。
/// </summary>
/// <remarks>
/// 表の見出しや形が変わったことを示す。照合テストはこれを失敗として扱い、「一致」とはみなさない。
/// </remarks>
/// <param name="message">文書名・行・直し方を含むメッセージ。</param>
internal sealed class MarkdownTableFormatException(string message) : Exception(message);
