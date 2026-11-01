using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Definition.Validation;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary>
/// States 形式の YAML に書いた <c>$.sys.now("…")</c> などの CallPath を、Loader と Level1 が受理・拒否することを検証する。
/// </summary>
/// <remarks>
/// <see cref="StateDefinition"/> を直接組み立てるテストでは Loader を通らないため、YAML から読み込む経路をここで固定する。
/// </remarks>
public sealed class SysCallPathDefinitionLoadTests
{
    /// <summary>input マップの値に書いた CallPath を path として読み込む。</summary>
    [Fact]
    public void Load_InputMapValueWithCallPath_ParsesAsPath()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  d: $.sys.now("yyyyMMdd")
            """);

        // Act
        var def = new StateWorkflowDefinitionLoader().Load(yaml);

        // Assert
        Assert.Equal("$.sys.now(\"yyyyMMdd\")", def.States["A"].Input!.Values!["d"].Path);
    }

    /// <summary>input.path に書いた CallPath を単一 path として読み込む。</summary>
    [Fact]
    public void Load_InputPathWithCallPath_ParsesAsSinglePath()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  path: $.sys.utcNow("HHmm")
            """);

        // Act
        var def = new StateWorkflowDefinitionLoader().Load(yaml);

        // Assert
        Assert.Equal("$.sys.utcNow(\"HHmm\")", def.States["A"].Input!.Path);
    }

    /// <summary>input のスカラーに書いた CallPath を単一 path として読み込む。</summary>
    [Fact]
    public void Load_InputScalarWithCallPath_ParsesAsSinglePath()
    {
        // Arrange
        var yaml = CreateYaml("""
                input: $.sys.now("yyyyMMdd")
            """);

        // Act
        var def = new StateWorkflowDefinitionLoader().Load(yaml);

        // Assert
        Assert.Equal("$.sys.now(\"yyyyMMdd\")", def.States["A"].Input!.Path);
    }

    /// <summary>ドットキーの値に書いた CallPath を path として読み込む。</summary>
    [Fact]
    public void Load_DottedKeyValueWithCallPath_ParsesAsPath()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  meta.day: $.sys.now("yyyyMMdd")
            """);

        // Act
        var def = new StateWorkflowDefinitionLoader().Load(yaml);

        // Assert
        Assert.Equal("$.sys.now(\"yyyyMMdd\")", def.States["A"].Input!.Values!["meta.day"].Path);
    }

    /// <summary>条件遷移の when.path に書いた CallPath を読み込み、Level1 も受理する。</summary>
    [Fact]
    public void Load_WhenPathWithCallPath_ParsesAndPassesLevel1()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              A:
                on:
                  Completed:
                    cases:
                      - when:
                          path: $.sys.now("yyyyMMdd")
                          op: eq
                          value: "20261101"
                        next: B
                    default: B
              B:
                on:
                  Completed:
                    end: true
            """;

        // Act
        var def = new StateWorkflowDefinitionLoader().Load(yaml);
        var result = Level1Validator.Validate(def);

        // Assert
        Assert.Equal("$.sys.now(\"yyyyMMdd\")", def.States["A"].On!["Completed"].Cases![0].When.Path);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    /// <summary>input に CallPath を書いた定義は Level1 も受理する。</summary>
    [Fact]
    public void Load_InputWithCallPath_PassesLevel1()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  d: $.sys.now("yyyyMMdd")
                  t: $.sys.utcNow("HHmm")
            """);

        // Act
        var result = Level1Validator.Validate(new StateWorkflowDefinitionLoader().Load(yaml));

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    /// <summary>許可されていない CallPath は、許可一覧を添えて読み込み時に拒否する。</summary>
    [Theory]
    [InlineData("$.sys.today(\"yyyyMMdd\")")]
    [InlineData("$.sys.now('yyyyMMdd')")]
    [InlineData("$.sys.now(\"\")")]
    [InlineData("$.sys.now(\"a\", \"b\")")]
    public void Load_InputMapValueWithInvalidCallPath_ThrowsWithAllowedHint(string path)
    {
        // Arrange
        // 引用符を含む値をそのまま渡すため、ブロックスカラーで書く。
        var yaml = CreateYaml($"""
                input:
                  d: >-
                    {path}
            """);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new StateWorkflowDefinitionLoader().Load(yaml));

        // Assert
        Assert.Contains("invalid input path for key 'd'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("allowed call paths are", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>引数が 65 文字以上の CallPath は読み込み時に拒否する。</summary>
    [Fact]
    public void Load_InputMapValueWithTooLongCallPathArgument_Throws()
    {
        // Arrange
        var format = new string('y', SysPathCall.MaxArgumentLength + 1);
        var yaml = CreateYaml($"""
                input:
                  d: $.sys.now("{format}")
            """);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new StateWorkflowDefinitionLoader().Load(yaml));

        // Assert
        Assert.Contains("allowed call paths are", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>許可されていない CallPath を when.path に書くと、許可一覧を添えて読み込み時に拒否する。</summary>
    [Fact]
    public void Load_WhenPathWithInvalidCallPath_ThrowsWithAllowedHint()
    {
        // Arrange
        var yaml = """
            workflow:
              name: W
            states:
              A:
                on:
                  Completed:
                    cases:
                      - when:
                          path: $.sys.today("yyyyMMdd")
                          op: eq
                          value: "20261101"
                        next: B
                    default: B
              B:
                on:
                  Completed:
                    end: true
            """;

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new StateWorkflowDefinitionLoader().Load(yaml));

        // Assert
        Assert.Contains("invalid when.path", ex.Message, StringComparison.Ordinal);
        Assert.Contains("allowed call paths are", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>通常の SimpleJsonPath は従来どおり受理する。</summary>
    [Fact]
    public void Load_InputWithSimpleJsonPath_ParsesAsPath()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  a: $.vars.a
                  b: $.states['a.b'].output
            """);

        // Act
        var def = new StateWorkflowDefinitionLoader().Load(yaml);

        // Assert
        Assert.Equal("$.vars.a", def.States["A"].Input!.Values!["a"].Path);
        Assert.Equal("$.states['a.b'].output", def.States["A"].Input!.Values!["b"].Path);
    }

    /// <summary>状態 A（指定した input を持つ）だけの States 形式 YAML を作る。</summary>
    /// <param name="inputBlock">状態 A の直下に置く <c>input</c> ブロック（インデント済み）。</param>
    private static string CreateYaml(string inputBlock) => $"""
        workflow:
          name: W
        states:
          A:
        {inputBlock}
            on:
              Completed:
                end: true
        """;
}
