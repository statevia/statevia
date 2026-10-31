namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Action Host クライアントの接続設定。</summary>
/// <remarks>
/// <para><see cref="BaseUrl"/> が 1 件のプールになる。複数プロセスにするときは <see cref="Endpoints"/> だけを使う。</para>
/// <para>両方に値がある構成は起動時に拒否する。どちらを使うかを暗黙では決めない。</para>
/// </remarks>
internal sealed class ActionHostClientOptions
{
    /// <summary>設定セクション名（Action Host サーバーと共有）。</summary>
    public const string SectionName = "Statevia:ActionHost";

    /// <summary>Action Host のベース URL（例: <c>http://localhost:5001</c>）。</summary>
    public string? BaseUrl { get; set; }

    /// <summary>プールの絶対 http(s) URI。<see cref="BaseUrl"/> と同時には設定しない。</summary>
    public List<string> Endpoints { get; set; } = [];

    /// <summary>呼び出し側が使うエンドポイント一覧を決める。</summary>
    /// <returns>空白以外の <see cref="Endpoints"/>。それが空なら <see cref="BaseUrl"/> 1 件。どちらも無ければ空。</returns>
    public IReadOnlyList<string> ResolveEndpoints()
    {
        var configured = (Endpoints ?? [])
            .Where(static endpoint => !string.IsNullOrWhiteSpace(endpoint))
            .Select(static endpoint => endpoint.Trim())
            .ToArray();
        if (configured.Length > 0)
        {
            return configured;
        }

        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return [BaseUrl.Trim()];
        }

        return [];
    }
}
