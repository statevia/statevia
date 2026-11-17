using Xunit;

namespace Statevia.Architecture.Tests.Docs;

/// <summary>
/// <see cref="MarkdownTableReader"/> が docs の表を読み、形が崩れたときに「一致」とみなさず失敗することを検証する。
/// </summary>
public sealed class MarkdownTableReaderTests
{
    private const string SourceName = "test.md";

    /// <summary>見出しの列のセルを、バッククォートを除いて行番号つきで返す。</summary>
    [Fact]
    public void ReadColumn_ReturnsCellsWithoutBackticks_WhenColumnExists()
    {
        // Arrange
        var markdown = string.Join('\n',
            "# 文書",
            "",
            "| Permission key | 表示ラベル |",
            "| --- | --- |",
            "| `definitions.read` | Read definitions |",
            "| `tenant.admin` | Tenant administration |");

        // Act
        var cells = MarkdownTableReader.ReadColumn(markdown, "Permission key", SourceName);

        // Assert
        Assert.Equal(
            [new MarkdownTableCell("definitions.read", "`definitions.read`", 5), new MarkdownTableCell("tenant.admin", "`tenant.admin`", 6)],
            cells);
    }

    /// <summary>同じ見出しの列を持つ表が複数あれば、すべての行を文書の順に返す。</summary>
    [Fact]
    public void ReadColumn_ReturnsCellsFromAllTables_WhenSeveralTablesHaveColumn()
    {
        // Arrange
        var markdown = string.Join('\n',
            "### 401",
            "",
            "| `error.code` | 典型状況 |",
            "| --- | --- |",
            "| `UNAUTHORIZED` | 認証なし |",
            "",
            "### 404",
            "",
            "| `error.code` | 典型状況 |",
            "| --- | --- |",
            "| `NOT_FOUND` | 未存在 |");

        // Act
        var cells = MarkdownTableReader.ReadColumn(markdown, "error.code", SourceName);

        // Assert
        Assert.Equal(["UNAUTHORIZED", "NOT_FOUND"], cells.Select(cell => cell.Text));
    }

    /// <summary>節を指定すると、その節の表だけを読み、次の同じ階層の見出しより後は読まない。</summary>
    [Fact]
    public void ReadColumn_ReadsOnlyTheSection_WhenSectionHeadingIsGiven()
    {
        // Arrange
        var markdown = string.Join('\n',
            "## 1. エンドポイント一覧",
            "",
            "| メソッド | パス |",
            "| --- | --- |",
            "| GET | /v1/health |",
            "",
            "### 1.1 補足",
            "",
            "| メソッド | パス |",
            "| --- | --- |",
            "| POST | /v1/events |",
            "",
            "## 2. 別の節",
            "",
            "| メソッド | パス |",
            "| --- | --- |",
            "| GET | /v1/other |");

        // Act
        var cells = MarkdownTableReader.ReadColumn(markdown, "パス", SourceName, "## 1. エンドポイント一覧");

        // Assert
        Assert.Equal(["/v1/health", "/v1/events"], cells.Select(cell => cell.Text));
    }

    /// <summary>セル内の <c>\|</c> は区切りとみなさず、CRLF の改行も読める。</summary>
    [Fact]
    public void ReadColumn_KeepsEscapedPipeAndReadsCrLf()
    {
        // Arrange
        var markdown = "| 値 | 説明 |\r\n| --- | --- |\r\n| a \\| b | 説明 |\r\n";

        // Act
        var cells = MarkdownTableReader.ReadColumn(markdown, "説明", SourceName);

        // Assert
        Assert.Equal(["説明"], cells.Select(cell => cell.Text));
    }

    /// <summary>コードブロックの中の表は読まない。</summary>
    [Fact]
    public void ReadColumn_IgnoresTablesInsideCodeBlocks()
    {
        // Arrange
        var markdown = string.Join('\n',
            "```markdown",
            "| 種別 | 値 |",
            "| --- | --- |",
            "| Guide | x |",
            "```",
            "",
            "| 種別 | 値 |",
            "| --- | --- |",
            "| Reference | y |");

        // Act
        var cells = MarkdownTableReader.ReadColumn(markdown, "種別", SourceName);

        // Assert
        Assert.Equal(["Reference"], cells.Select(cell => cell.Text));
    }

    /// <summary>見出しの列を持つ表が無ければ、一致とみなさず例外を投げる。</summary>
    [Fact]
    public void ReadColumn_Throws_WhenNoTableHasColumn()
    {
        // Arrange
        var markdown = string.Join('\n', "| 別の列 | 値 |", "| --- | --- |", "| a | b |");

        // Act
        var exception = Assert.Throws<MarkdownTableFormatException>(() => MarkdownTableReader.ReadColumn(markdown, "Permission key", SourceName));

        // Assert
        Assert.Contains("「Permission key」列を持つ表が見つかりません", exception.Message);
    }

    /// <summary>列が足りない行があれば、その行番号つきで例外を投げる。</summary>
    [Fact]
    public void ReadColumn_Throws_WhenRowLacksColumn()
    {
        // Arrange
        var markdown = string.Join('\n', "| 値 | 説明 |", "| --- | --- |", "| a | b |", "| c |");

        // Act
        var exception = Assert.Throws<MarkdownTableFormatException>(() => MarkdownTableReader.ReadColumn(markdown, "説明", SourceName));

        // Assert
        Assert.StartsWith("test.md:4:", exception.Message);
    }

    /// <summary>表の本体が空なら例外を投げる。</summary>
    [Fact]
    public void ReadColumn_Throws_WhenTableIsEmpty()
    {
        // Arrange
        var markdown = string.Join('\n', "| 値 | 説明 |", "| --- | --- |", "", "本文");

        // Act
        var exception = Assert.Throws<MarkdownTableFormatException>(() => MarkdownTableReader.ReadColumn(markdown, "値", SourceName));

        // Assert
        Assert.Contains("行がありません", exception.Message);
    }

    /// <summary>指定した見出しの節が無ければ例外を投げる。</summary>
    [Fact]
    public void ReadColumn_Throws_WhenSectionHeadingIsMissing()
    {
        // Arrange
        var markdown = string.Join('\n', "## 別の節", "", "| 値 |", "| --- |", "| a |");

        // Act
        var exception = Assert.Throws<MarkdownTableFormatException>(
            () => MarkdownTableReader.ReadColumn(markdown, "値", SourceName, "## 1. エンドポイント一覧"));

        // Assert
        Assert.Contains("見出し「## 1. エンドポイント一覧」が見つかりません", exception.Message);
    }
}
