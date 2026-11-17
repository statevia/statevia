using Xunit;

namespace Statevia.Architecture.Tests.Docs;

/// <summary>
/// <see cref="DocsConsistencyExclusions"/> が理由の無い除外や重複を受け付けないことを検証する。
/// </summary>
public sealed class DocsConsistencyExclusionsTests
{
    private const string SourceName = "exclusions.json";

    /// <summary>リポジトリの除外一覧は検証を通る。</summary>
    [Fact]
    public void Load_Succeeds_ForRepositoryFile()
    {
        // Arrange
        // リポジトリの docs-consistency-exclusions.json を読む。

        // Act
        var exclusions = DocsConsistencyExclusions.Load();

        // Assert
        Assert.Equal(DocsConsistencyExclusions.KnownCategories.Order(), exclusions.Categories.Keys.Order());
    }

    /// <summary>理由つきの項目を読み、書かれていない領域は空の一覧になる。</summary>
    [Fact]
    public void Parse_ReturnsEntries_AndEmptyListForMissingCategory()
    {
        // Arrange
        const string json = """
            {
              "apiRoutes": [
                { "value": "POST /internal/modules/reload", "reason": "運用者向けの内部 API で、利用者向けの契約に載せない" }
              ]
            }
            """;

        // Act
        var exclusions = DocsConsistencyExclusions.Parse(json, SourceName);

        // Assert
        Assert.Equal(["POST /internal/modules/reload"], exclusions.ValuesFor(DocsConsistencyExclusions.ApiRoutes));
        Assert.Empty(exclusions.For(DocsConsistencyExclusions.UiRoutes));
    }

    /// <summary>理由が空の項目があれば、領域と位置つきで例外を投げる。</summary>
    [Fact]
    public void Parse_Throws_WhenReasonIsEmpty()
    {
        // Arrange
        const string json = """{ "uiRoutes": [ { "value": "/login", "reason": " " } ] }""";

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => DocsConsistencyExclusions.Parse(json, SourceName));

        // Assert
        Assert.Contains("uiRoutes[0]: reason が空です", exception.Message);
    }

    /// <summary>理由の項目自体が無くても、空とみなして例外を投げる。</summary>
    [Fact]
    public void Parse_Throws_WhenReasonIsMissing()
    {
        // Arrange
        const string json = """{ "errorCodes": [ { "value": "START_FAILED" } ] }""";

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => DocsConsistencyExclusions.Parse(json, SourceName));

        // Assert
        Assert.Contains("errorCodes[0]: reason が空です", exception.Message);
    }

    /// <summary>同じ領域で値が重複していれば例外を投げる。</summary>
    [Fact]
    public void Parse_Throws_WhenValueIsDuplicated()
    {
        // Arrange
        const string json = """
            {
              "permissionKeys": [
                { "value": "modules.read", "reason": "理由 1" },
                { "value": "modules.read", "reason": "理由 2" }
              ]
            }
            """;

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => DocsConsistencyExclusions.Parse(json, SourceName));

        // Assert
        Assert.Contains("value「modules.read」が重複しています", exception.Message);
    }

    /// <summary>未知の領域があれば例外を投げる（綴りの誤りで除外が効かないのを防ぐ）。</summary>
    [Fact]
    public void Parse_Throws_WhenCategoryIsUnknown()
    {
        // Arrange
        const string json = """{ "apiRoute": [] }""";

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => DocsConsistencyExclusions.Parse(json, SourceName));

        // Assert
        Assert.Contains("未知の領域「apiRoute」", exception.Message);
    }

    /// <summary>JSON として読めなければ例外を投げる。</summary>
    [Fact]
    public void Parse_Throws_WhenJsonIsMalformed()
    {
        // Arrange
        const string json = """{ "apiRoutes": [ """;

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => DocsConsistencyExclusions.Parse(json, SourceName));

        // Assert
        Assert.Contains("JSON として読めません", exception.Message);
    }
}
