namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Container がマウントするテナントディレクトリを決めるため、tenant_key を読む。</summary>
internal interface ISandboxTenantKeyLookup
{
    /// <summary>テナント行の tenant_key を返す。</summary>
    /// <param name="tenantId">実行リクエストのテナント ID。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>行があるときの tenant_key。行が無いときは null。</returns>
    Task<string?> FindTenantKeyAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary><see cref="IPrincipalDataAccess"/> から、その実行の tenant_key だけを読む。</summary>
/// <remarks>
/// <para>データアクセスは Scoped のため、Singleton のサンドボックスから呼び出しごとにスコープを開く。</para>
/// <para>全テナントの事前読み込みはしない。</para>
/// </remarks>
/// <param name="scopes">呼び出しごとのスコープ。</param>
internal sealed class SandboxTenantKeyLookup(IServiceScopeFactory scopes) : ISandboxTenantKeyLookup
{
    /// <inheritdoc />
    public async Task<string?> FindTenantKeyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var principals = scope.ServiceProvider.GetRequiredService<IPrincipalDataAccess>();
        var tenant = await principals.FindTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return tenant?.TenantKey;
    }
}
