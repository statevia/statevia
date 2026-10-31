namespace Statevia.Infrastructure.Persistence;

/// <summary>テナントの変数またはシークレットを 1 行で表す。</summary>
/// <remarks>
/// <para>管理 API と実行時解決から使う。値は定義 YAML と workflow input には置かない。</para>
/// <para><c>kind=variable</c> は平文列だけ、<c>kind=secret</c> は保護列だけを持つ。両方に値を入れない。</para>
/// </remarks>
internal sealed class TenantActionBindingRow
{
    /// <summary>行の識別子。保護 API の用途に使う。</summary>
    public Guid BindingId { get; set; }

    /// <summary>オーナーテナント。</summary>
    public Guid TenantId { get; set; }

    /// <summary>テナント内で一意な名前。識別子 1〜64。</summary>
    public string Name { get; set; } = "";

    /// <summary><c>variable</c> または <c>secret</c>。</summary>
    public string Kind { get; set; } = "";

    /// <summary>変数の値。シークレットのときは null。</summary>
    public string? ValuePlaintext { get; set; }

    /// <summary>保護 API の暗号文。変数のときは null。</summary>
    public string? ValueProtected { get; set; }

    /// <summary>作成時刻（UTC）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最終更新時刻（UTC）。</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>この行を受け取ってよい canonical actionId。</summary>
    public List<TenantActionBindingActionRow> AllowedActions { get; set; } = [];

    /// <summary>変数として平文だけを置く。保護列は空にする。</summary>
    /// <param name="plaintext">空白のみでない値。</param>
    public void SetVariable(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        Kind = TenantActionBindingKinds.Variable;
        ValuePlaintext = plaintext;
        ValueProtected = null;
    }

    /// <summary>シークレットとして保護文字列だけを置く。平文列は空にする。</summary>
    /// <param name="protectedValue">保護 API の出力。</param>
    public void SetSecret(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
        Kind = TenantActionBindingKinds.Secret;
        ValuePlaintext = null;
        ValueProtected = protectedValue;
    }
}

/// <summary>バインディング行を受け取ってよい canonical actionId。</summary>
internal sealed class TenantActionBindingActionRow
{
    /// <summary>親行。</summary>
    public Guid BindingId { get; set; }

    /// <summary>canonical actionId。</summary>
    public string ActionId { get; set; } = "";

    /// <summary>親行。</summary>
    public TenantActionBindingRow? Binding { get; set; }
}

/// <summary>バインディング種別の保存値。</summary>
internal static class TenantActionBindingKinds
{
    /// <summary>非秘密の変数。</summary>
    public const string Variable = "variable";

    /// <summary>保護 API で保存するシークレット。</summary>
    public const string Secret = "secret";
}
