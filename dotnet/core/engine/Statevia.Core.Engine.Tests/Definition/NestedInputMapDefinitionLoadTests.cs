using Statevia.Core.Engine.Definition;
using System.Collections;
using Xunit;

namespace Statevia.Core.Engine.Tests.Definition;

/// <summary>
/// States 形式の YAML の <c>input</c> に書いたネスト map を、Loader がドットキーへ展開し、トップレベルと同じ規則で検証することを確かめる。
/// </summary>
/// <remarks>
/// <see cref="StateDefinition"/> を直接組み立てるテストでは Loader を通らないため、YAML から読み込む経路をここで固定する。
/// </remarks>
public sealed class NestedInputMapDefinitionLoadTests
{
    /// <summary>1 段のネスト map の中のパス式は path、リテラルは literal のドットキーになり、親キーは残らない。</summary>
    [Fact]
    public void Load_NestedMap_FlattensToDottedPathAndLiteral()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  body:
                    orderId: $.states.Prepare.output.orderId
                    currency: JPY
            """);

        // Act
        var values = LoadInputValues(yaml);

        // Assert
        Assert.Equal("$.states.Prepare.output.orderId", values["body.orderId"].Path);
        Assert.Null(values["body.orderId"].Literal);
        Assert.Equal("JPY", values["body.currency"].Literal);
        Assert.Null(values["body.currency"].Path);
        Assert.False(values.ContainsKey("body"));
    }

    /// <summary>2 段以上のネストも、論理キーを連結したドットキーになる。</summary>
    [Fact]
    public void Load_TwoLevelNestedMap_FlattensToDeepDottedKey()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  ship:
                    contact:
                      email: $.input.email
            """);

        // Act
        var values = LoadInputValues(yaml);

        // Assert
        Assert.Equal("$.input.email", values["ship.contact.email"].Path);
    }

    /// <summary>ネストの中の数値・真偽値・null は、型を保ったまま literal になる。</summary>
    [Fact]
    public void Load_NestedMapScalarLiterals_KeepTypes()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  options:
                    retries: 3
                    dryRun: true
                    note: null
            """);

        // Act
        var values = LoadInputValues(yaml);

        // Assert
        Assert.Equal(3, Convert.ToInt32(values["options.retries"].Literal, System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(Assert.IsType<bool>(values["options.dryRun"].Literal));
        Assert.Null(values["options.note"].Literal);
        Assert.Null(values["options.note"].Path);
    }

    /// <summary>空の map は展開せず、空の辞書の literal のまま残る。</summary>
    [Fact]
    public void Load_EmptyNestedMap_StaysEmptyLiteral()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  body: {}
            """);

        // Act
        var values = LoadInputValues(yaml);

        // Assert
        var literal = Assert.IsAssignableFrom<IDictionary>(values["body"].Literal);
        Assert.Empty(literal);
    }

    /// <summary>ネスト map のキーにドットがあれば、ドットキーと同じくネストの区切りとして扱う。</summary>
    [Fact]
    public void Load_NestedMapKeyWithDot_JoinsAsDottedKey()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  headers:
                    X.Trace: $.input.traceId
            """);

        // Act
        var values = LoadInputValues(yaml);

        // Assert
        Assert.Equal("$.input.traceId", values["headers.X.Trace"].Path);
    }

    /// <summary>ネストの中の CallPath も path として読み込む。</summary>
    [Fact]
    public void Load_NestedMapCallPath_ParsesAsPath()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  body:
                    day: $.sys.now("yyyyMMdd")
            """);

        // Act
        var values = LoadInputValues(yaml);

        // Assert
        Assert.Equal("$.sys.now(\"yyyyMMdd\")", values["body.day"].Path);
    }

    /// <summary>ネストの中の <c>${...}</c> は、トップレベルと同じメッセージで拒否する。</summary>
    [Fact]
    public void Load_NestedMapTemplate_Throws()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  body:
                    orderId: ${orderId}
            """);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new StateWorkflowDefinitionLoader().Load(yaml));

        // Assert
        Assert.Contains("'${...}' input templates are not supported", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ネストの中の不正なパスは、論理キーを示して拒否する。</summary>
    [Fact]
    public void Load_NestedMapInvalidPath_ThrowsWithLogicalKey()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  body:
                    orderId: $.vars[*]
            """);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new StateWorkflowDefinitionLoader().Load(yaml));

        // Assert
        Assert.Contains("invalid input path for key 'body.orderId'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ネスト map の中の空のキーは拒否する。</summary>
    [Fact]
    public void Load_NestedMapEmptyKey_Throws()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  body:
                    "": x
            """);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new StateWorkflowDefinitionLoader().Load(yaml));

        // Assert
        Assert.Contains("input key 'body' contains an empty child key.", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>ネスト map とドットキーで同じ論理キーを 2 回書くと拒否する（大文字小文字は区別しない）。</summary>
    [Fact]
    public void Load_NestedMapAndDottedKeyDuplicate_Throws()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  body:
                    orderId: $.input.a
                  Body.OrderId: $.input.b
            """);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new StateWorkflowDefinitionLoader().Load(yaml));

        // Assert
        Assert.Contains("duplicate input key", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("$.input", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>スカラーと子を同じ論理位置に書くと、action が無い状態でも拒否する。</summary>
    [Fact]
    public void Load_ScalarAndNestedChildConflict_Throws()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  ship: secret-literal
                  ship.address: $.input.address
            """);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new StateWorkflowDefinitionLoader().Load(yaml));

        // Assert
        Assert.Contains("input key 'ship' conflicts with 'ship.address'.", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-literal", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>セグメント単位で比べるので、<c>ship</c> と <c>shipping.x</c> は競合としない。</summary>
    [Fact]
    public void Load_KeysSharingTextPrefixOnly_DoNotConflict()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  ship: a
                  shipping:
                    x: b
            """);

        // Act
        var values = LoadInputValues(yaml);

        // Assert
        Assert.Equal("a", values["ship"].Literal);
        Assert.Equal("b", values["shipping.x"].Literal);
    }

    /// <summary>配列の要素や配列の中の map にパス式を書くと、論理パスを示して拒否する。</summary>
    [Theory]
    [InlineData("""
                input:
                  items:
                    - $.input.a
                    - $.input.b
            """, "input key 'items[0]': path expressions are not allowed inside arrays.")]
    [InlineData("""
                input:
                  body:
                    lines:
                      - sku: A-1
                      - sku: $.input.sku
            """, "input key 'body.lines[1].sku': path expressions are not allowed inside arrays.")]
    public void Load_PathExpressionInsideArray_ThrowsWithLogicalPath(string inputBlock, string expectedMessage)
    {
        // Arrange
        var yaml = CreateYaml(inputBlock);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => new StateWorkflowDefinitionLoader().Load(yaml));

        // Assert
        Assert.Contains(expectedMessage, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>リテラルだけの配列（<c>$100</c> のようにパス式でない文字列を含む）は literal のまま受理する。</summary>
    [Fact]
    public void Load_LiteralOnlyArray_StaysLiteral()
    {
        // Arrange
        var yaml = CreateYaml("""
                input:
                  prices:
                    - $100
                    - 3
                    - name: x
            """);

        // Act
        var values = LoadInputValues(yaml);

        // Assert
        var literal = Assert.IsAssignableFrom<IList>(values["prices"].Literal);
        Assert.Equal(3, literal.Count);
        Assert.Equal("$100", literal[0]);
    }

    /// <summary>YAML を読み込み、状態 A の input values を返す。</summary>
    /// <param name="yaml">定義 YAML。</param>
    private static IReadOnlyDictionary<string, StateInputValueDefinition> LoadInputValues(string yaml)
    {
        var def = new StateWorkflowDefinitionLoader().Load(yaml);
        var values = def.States["A"].Input?.Values;
        Assert.NotNull(values);
        return values;
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
