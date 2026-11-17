using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Statevia.Service.Api.Tests.OpenApi;

/// <summary>
/// Swashbuckle が生成する OpenAPI ドキュメントのスモークテスト。
/// </summary>
public sealed class OpenApiDocumentTests : IClassFixture<StateviaApiWebApplicationFactory>
{
    /// <summary>OpenAPI の Path Item のうち、HTTP メソッドを表すキー。</summary>
    private static readonly HashSet<string> HttpMethodKeys = ["get", "post", "put", "patch", "delete", "head", "options"];

    private readonly StateviaApiWebApplicationFactory _factory;

    /// <summary>
    /// <see cref="OpenApiDocumentTests"/> を生成する。
    /// </summary>
    /// <param name="factory">テスト用 Web アプリケーションファクトリ。</param>
    public OpenApiDocumentTests(StateviaApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Development 環境で swagger.json が取得でき、主要パスとスキーマが含まれる。
    /// </summary>
    [Fact]
    public async Task SwaggerJson_ContainsRequiredPathsAndSchemas()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("paths", out var paths));
        AssertRequiredPath(paths, "/v1/health");
        AssertRequiredPath(paths, "/v1/definitions");
        AssertRequiredPath(paths, "/v1/definitions/validate");
        AssertRequiredPath(paths, "/v1/executions");
        AssertRequiredPath(paths, "/v1/executions/{executionId}/stream");

        Assert.True(root.TryGetProperty("components", out var components));
        Assert.True(components.TryGetProperty("schemas", out var schemas));
        Assert.True(schemas.TryGetProperty("ErrorResponse", out _));
        Assert.True(schemas.TryGetProperty("ExecutionResponse", out _));
        Assert.True(ContainsPagedResultSchema(schemas));
    }

    /// <summary>
    /// 環境変数 <c>STATEVIA_EXPORT_OPENAPI</c> 指定時にリポジトリへ OpenAPI JSON を書き出す（手動 / スクリプト用）。
    /// </summary>
    [Fact]
    public async Task ExportOpenApiToRepository_WhenExportFlagSet()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("STATEVIA_EXPORT_OPENAPI"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Arrange
        using var client = _factory.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));
        var json = await response.Content.ReadAsStringAsync();

        var outputPath = CommittedOpenApiPath;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, json);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(File.Exists(outputPath));
    }

    /// <summary>
    /// リポジトリにコミットした OpenAPI の操作（メソッドとパス）が、今の実装が生成する OpenAPI と一致する。
    /// </summary>
    /// <remarks>
    /// コミット済みの JSON は、文書と実装の照合テスト（<c>Statevia.Architecture.Tests</c> の <c>ApiRouteDocsConsistencyTests</c>）が
    /// 実装の正本として読む。書き出しを忘れると照合テストが古い JSON と比べて通ってしまうので、ここで鮮度を確かめる。
    /// スキーマの細部まで一致は求めない（操作の集合だけを比べる）。
    /// </remarks>
    [Fact]
    public async Task CommittedOpenApi_HasSameOperationsAsGenerated()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var committedJson = await File.ReadAllTextAsync(CommittedOpenApiPath);

        // Act
        using var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));
        var generatedJson = await response.Content.ReadAsStringAsync();
        var generated = ReadOperations(generatedJson);
        var committed = ReadOperations(committedJson);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var missingFromCommitted = generated.Except(committed).Order(StringComparer.Ordinal).ToList();
        var missingFromGenerated = committed.Except(generated).Order(StringComparer.Ordinal).ToList();
        Assert.True(
            missingFromCommitted.Count == 0 && missingFromGenerated.Count == 0,
            "コミット済みの core-api-v1.openapi.json が実装と一致しません。リポジトリルートで "
            + "scripts/export-core-api-openapi.ps1 を実行して書き出し直してください。\n"
            + "実装にあってコミット済みに無い: " + string.Join(", ", missingFromCommitted) + "\n"
            + "コミット済みにあって実装に無い: " + string.Join(", ", missingFromGenerated));
    }

    /// <summary>コミット済みの OpenAPI の絶対パス（<c>dotnet/service/api/openapi/core-api-v1.openapi.json</c>）。</summary>
    /// <remarks>テストの出力（<c>Statevia.Service.Api.Tests/bin/{構成}/{TFM}/</c>）から 6 段上が <c>dotnet/</c>。</remarks>
    private static string CommittedOpenApiPath =>
        Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..")),
            "service",
            "api",
            "openapi",
            "core-api-v1.openapi.json");

    /// <summary>OpenAPI の JSON から <c>METHOD /path</c> の集合を読む。</summary>
    /// <param name="json">OpenAPI の JSON。</param>
    /// <returns>操作の集合。</returns>
    private static HashSet<string> ReadOperations(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value
                .EnumerateObject()
                .Where(operation => HttpMethodKeys.Contains(operation.Name))
                .Select(operation => $"{operation.Name.ToUpperInvariant()} {path.Name}"))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void AssertRequiredPath(JsonElement paths, string path)
    {
        Assert.True(
            paths.TryGetProperty(path, out _),
            string.Create(CultureInfo.InvariantCulture, $"OpenAPI paths に '{path}' がありません。"));
    }

    private static bool ContainsPagedResultSchema(JsonElement schemas)
    {
        foreach (var property in schemas.EnumerateObject())
        {
            if (property.Name.Contains("PagedResult", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
