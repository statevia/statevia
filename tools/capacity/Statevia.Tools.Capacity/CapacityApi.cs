using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Statevia.Tools.Capacity;

/// <summary>容量計測向けの Service API クライアント。トークンをログに出さない。</summary>
internal sealed class CapacityApi : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;

    /// <summary>HTTP クライアントを基点 URL で初期化する。</summary>
    /// <param name="baseUrl">末尾スラッシュの有無は問わない。</param>
    /// <param name="requestTimeout">1 リクエストの上限。</param>
    public CapacityApi(Uri baseUrl, TimeSpan requestTimeout)
    {
        var builder = new UriBuilder(baseUrl);
        if (!builder.Path.EndsWith('/'))
        {
            builder.Path += "/";
        }

        _http = new HttpClient
        {
            BaseAddress = builder.Uri,
            Timeout = requestTimeout,
        };
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    /// <summary>ヘルスチェック。2xx 以外は失敗。</summary>
    public async Task EnsureHealthyAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(new Uri("v1/health", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GET /v1/health returned {(int)response.StatusCode}.");
        }
    }

    /// <summary>ログインして Bearer をセットする。</summary>
    public async Task LoginAsync(string tenantKey, string username, string password, CancellationToken cancellationToken)
    {
        var payload = new LoginRequest(tenantKey, username, password);
        using var response = await _http.PostAsJsonAsync(
                new Uri("v1/auth/login", UriKind.Relative),
                payload,
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"POST /v1/auth/login returned {(int)response.StatusCode}.");
        }

        var parsed = JsonSerializer.Deserialize<LoginResponse>(body, JsonOptions)
            ?? throw new HttpRequestException("POST /v1/auth/login returned an empty body.");
        if (string.IsNullOrWhiteSpace(parsed.AccessToken))
        {
            throw new HttpRequestException("POST /v1/auth/login did not return accessToken.");
        }

        ApplyBearer(parsed.AccessToken, tenantKey);
    }

    /// <summary>既存トークンまたは API キーをヘッダへ載せる。</summary>
    public void ApplyPrincipal(string tenantKey, string? accessToken, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Remove("X-Api-Key");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);
            ApplyTenant(tenantKey);
            return;
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            ApplyBearer(accessToken, tenantKey);
        }
    }

    /// <summary>定義を登録し displayId を返す。</summary>
    public async Task<string> CreateDefinitionAsync(string name, string yaml, CancellationToken cancellationToken)
    {
        var payload = new DefinitionCreateRequest(name, yaml);
        using var response = await SendJsonAsync(HttpMethod.Post, "v1/definitions", payload, withIdempotency: false, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"POST /v1/definitions returned {(int)response.StatusCode}.");
        }

        var parsed = JsonSerializer.Deserialize<DisplayIdResponse>(body, JsonOptions)
            ?? throw new HttpRequestException("POST /v1/definitions returned an empty body.");
        if (string.IsNullOrWhiteSpace(parsed.DisplayId))
        {
            throw new HttpRequestException("POST /v1/definitions did not return displayId.");
        }

        return parsed.DisplayId;
    }

    /// <summary>実行を開始する。</summary>
    /// <returns>HTTP ステータス、displayId（成功時）、所要ミリ秒。</returns>
    public async Task<StartAttempt> StartExecutionAsync(string definitionId, CancellationToken cancellationToken)
    {
        var payload = new StartExecutionRequest(definitionId);
        var started = Stopwatch.StartNew();
        using var response = await SendJsonAsync(HttpMethod.Post, "v1/executions", payload, withIdempotency: true, cancellationToken)
            .ConfigureAwait(false);
        started.Stop();
        var statusCode = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            return new StartAttempt(statusCode, DisplayId: null, started.Elapsed.TotalMilliseconds, Is5xx: statusCode >= 500);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize<DisplayIdResponse>(body, JsonOptions);
        return new StartAttempt(
            statusCode,
            parsed?.DisplayId,
            started.Elapsed.TotalMilliseconds,
            Is5xx: false);
    }

    /// <summary>実行の投影 status を返す。404 は null。</summary>
    public async Task<string?> GetExecutionStatusAsync(string executionDisplayId, CancellationToken cancellationToken)
    {
        var path = $"v1/executions/{Uri.EscapeDataString(executionDisplayId)}";
        using var response = await _http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize<StatusResponse>(body, JsonOptions);
        return parsed?.Status;
    }

    /// <summary>未完了 Wait 一覧。グラフ未準備の 404 は空配列扱い。</summary>
    public async Task<IReadOnlyList<WaitItem>> GetWaitsAsync(string executionDisplayId, CancellationToken cancellationToken)
    {
        var path = $"v1/executions/{Uri.EscapeDataString(executionDisplayId)}/waits";
        using var response = await _http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize<WaitsResponse>(body, JsonOptions);
        if (parsed is null)
        {
            return [];
        }

        return parsed.Waits
            .Select(static item => new WaitItem(item.NodeId, item.NodeName, item.AllowedEvents ?? []))
            .ToArray();
    }

    /// <summary>GET graph の HTTP ステータス。投影の非破壊確認用。</summary>
    public async Task<int> GetGraphStatusCodeAsync(string executionDisplayId, CancellationToken cancellationToken)
    {
        var path = $"v1/executions/{Uri.EscapeDataString(executionDisplayId)}/graph";
        using var response = await _http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return (int)response.StatusCode;
    }

    /// <summary>実行を Cancel する。本文は空 JSON。</summary>
    public async Task<CancelAttempt> CancelExecutionAsync(string executionDisplayId, CancellationToken cancellationToken)
    {
        var path = $"v1/executions/{Uri.EscapeDataString(executionDisplayId)}/cancel";
        var started = Stopwatch.StartNew();
        using var response = await SendJsonAsync(HttpMethod.Post, path, new EmptyJsonBody(), withIdempotency: true, cancellationToken)
            .ConfigureAwait(false);
        started.Stop();
        var statusCode = (int)response.StatusCode;
        return new CancelAttempt(statusCode, started.Elapsed.TotalMilliseconds, Is5xx: statusCode >= 500);
    }

    /// <summary>ヘルスが 2xx なら true。接続拒否やタイムアウトは false。</summary>
    public async Task<bool> TryEnsureHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureHealthyAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }

    /// <summary>ノード Resume を送る。</summary>
    public async Task<ResumeAttempt> ResumeNodeAsync(
        string executionDisplayId,
        string nodeId,
        string resumeKey,
        CancellationToken cancellationToken)
    {
        var path = $"v1/executions/{Uri.EscapeDataString(executionDisplayId)}/nodes/{Uri.EscapeDataString(nodeId)}/resume";
        var payload = new ResumeRequest(resumeKey);
        var started = Stopwatch.StartNew();
        using var response = await SendJsonAsync(HttpMethod.Post, path, payload, withIdempotency: true, cancellationToken)
            .ConfigureAwait(false);
        started.Stop();
        var statusCode = (int)response.StatusCode;
        string? errorCode = null;
        string? errorMessage = null;
        if (statusCode is < 200 or >= 300)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            (errorCode, errorMessage) = CapacityApiErrorParser.Parse(body);
        }

        return new ResumeAttempt(statusCode, started.Elapsed.TotalMilliseconds, Is5xx: statusCode >= 500, errorCode, errorMessage);
    }

    private void ApplyBearer(string accessToken, string tenantKey)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        ApplyTenant(tenantKey);
    }

    private void ApplyTenant(string tenantKey)
    {
        _http.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Tenant-Id", tenantKey);
    }

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string relativePath,
        object payload,
        bool withIdempotency,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(relativePath, UriKind.Relative))
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        if (withIdempotency)
        {
            request.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        }

        return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal sealed record StartAttempt(int StatusCode, string? DisplayId, double LatencyMilliseconds, bool Is5xx);

    /// <summary>ノード Resume 1 回分。非 2xx ではサニタイズ済みの code / message を付ける。</summary>
    internal sealed record ResumeAttempt(
        int StatusCode,
        double LatencyMilliseconds,
        bool Is5xx,
        string? ErrorCode = null,
        string? ErrorMessage = null);

    /// <summary>Cancel 1 回分。</summary>
    internal sealed record CancelAttempt(int StatusCode, double LatencyMilliseconds, bool Is5xx);

    internal sealed record WaitItem(string NodeId, string NodeName, IReadOnlyList<string> AllowedEvents);

    private sealed record LoginRequest(string TenantKey, string Username, string Password);

    private sealed record LoginResponse(string AccessToken);

    private sealed record DefinitionCreateRequest(string Name, string Yaml);

    private sealed record DisplayIdResponse(string DisplayId);

    private sealed record StartExecutionRequest(string DefinitionId);

    private sealed record EmptyJsonBody();

    private sealed record StatusResponse(string Status);

    private sealed record ResumeRequest(string ResumeKey);

    private sealed class WaitsResponse
    {
        public List<WaitItemDto> Waits { get; set; } = [];
    }

    private sealed class WaitItemDto
    {
        public string NodeId { get; set; } = string.Empty;

        public string NodeName { get; set; } = string.Empty;

        public List<string> AllowedEvents { get; set; } = [];
    }
}
