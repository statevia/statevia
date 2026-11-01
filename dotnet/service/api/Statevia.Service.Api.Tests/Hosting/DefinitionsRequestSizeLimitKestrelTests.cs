using Statevia.Core.Application.Contracts.Validation;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>定義エンドポイントの <c>[RequestSizeLimit]</c> を実 Kestrel 上で確かめる。</summary>
/// <remarks>
/// TestServer はリクエストボディ上限（<c>IHttpMaxRequestBodySizeFeature</c>）を実装しないため、
/// 本クラスだけ専用のファクトリを Kestrel で起動する。
/// </remarks>
public sealed class DefinitionsRequestSizeLimitKestrelTests : IDisposable
{
    private readonly SecurityIntegrationWebApplicationFactory _factory;

    /// <summary>Kestrel で待ち受けるファクトリを起動する。</summary>
    public DefinitionsRequestSizeLimitKestrelTests()
    {
        _factory = new SecurityIntegrationWebApplicationFactory();
        _factory.UseKestrel(0);
        _factory.StartServer();
    }

    /// <summary>validate のボディが上限を超えると 413 PAYLOAD_TOO_LARGE になり、500 にならない。</summary>
    [Fact]
    public async Task PostValidate_BodyExceedsRequestSizeLimit_ReturnsPayloadTooLarge()
    {
        // Arrange
        using var client = await CreateWriterClientAsync("kestrel-validate-too-large@example.com");
        using var content = CreateOversizedBody();

        // Act
        using var response = await client.PostAsync(new Uri("/v1/definitions/validate", UriKind.Relative), content);

        // Assert
        await AssertPayloadTooLargeAsync(response);
    }

    /// <summary>create のボディが上限を超えると 413 PAYLOAD_TOO_LARGE になる。</summary>
    [Fact]
    public async Task PostCreate_BodyExceedsRequestSizeLimit_ReturnsPayloadTooLarge()
    {
        // Arrange
        using var client = await CreateWriterClientAsync("kestrel-create-too-large@example.com");
        using var content = CreateOversizedBody();

        // Act
        using var response = await client.PostAsync(new Uri("/v1/definitions", UriKind.Relative), content);

        // Assert
        await AssertPayloadTooLargeAsync(response);
    }

    /// <summary>publish のボディが上限を超えると 413 PAYLOAD_TOO_LARGE になる（定義の存在確認より前に打ち切る）。</summary>
    [Fact]
    public async Task PutUpdate_BodyExceedsRequestSizeLimit_ReturnsPayloadTooLarge()
    {
        // Arrange
        using var client = await CreateWriterClientAsync("kestrel-update-too-large@example.com");
        using var content = CreateOversizedBody();

        // Act
        using var response = await client.PutAsync(new Uri("/v1/definitions/missing", UriKind.Relative), content);

        // Assert
        await AssertPayloadTooLargeAsync(response);
    }

    /// <summary>JSON エスケープで 6 倍に膨らむ制御文字だけの上限ちょうどの yaml は、ボディ上限では弾かれない。</summary>
    [Fact]
    public async Task PostValidate_EscapedYamlAtMaxBytes_IsNotRejectedByBodyLimit()
    {
        // Arrange
        using var client = await CreateWriterClientAsync("kestrel-validate-escaped@example.com");
        var yaml = new string('\u0001', DefinitionYamlConstraints.MaxUtf8Bytes);

        // Act
        using var response = await client.PostAsJsonAsync(
            new Uri("/v1/definitions/validate", UriKind.Relative),
            new { yaml });

        // Assert（中身は不正な YAML なので 422 になる）
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    /// <summary>yaml だけでボディ上限に達する JSON ボディを作る。</summary>
    private static JsonContent CreateOversizedBody()
    {
        var yaml = new string('a', (int)DefinitionYamlConstraints.MaxRequestBodyBytes);
        return JsonContent.Create(new { name = "order", yaml, projectId = Guid.NewGuid() });
    }

    private static async Task AssertPayloadTooLargeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "PAYLOAD_TOO_LARGE",
            document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private async Task<HttpClient> CreateWriterClientAsync(string loginIdentifier)
    {
        var principalId = await _factory.SeedUserWithPermissionsAsync(
            loginIdentifier,
            "password",
            WellKnownPermissionKeys.DefinitionsWrite);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _factory.IssueBearerToken(principalId));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");
        // 上限超過のボディを送り切る前に Kestrel が 413 を返して接続を閉じると、送信側で接続リセットになり得る。
        // 100-continue でボディ送信前に応答を受け取り、413 の判定を安定させる。
        client.DefaultRequestHeaders.ExpectContinue = true;
        return client;
    }
}
