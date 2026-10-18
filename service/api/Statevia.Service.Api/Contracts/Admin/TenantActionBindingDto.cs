using System.Text.Json.Serialization;

namespace Statevia.Service.Api.Contracts.Admin;

/// <summary>自テナントの Action バインディング 1 件。シークレットの値は含まない。</summary>
public sealed class TenantActionBindingDto
{
    /// <summary>テナント内で一意な名前。</summary>
    public string Name { get; init; } = "";

    /// <summary><c>variable</c> または <c>secret</c>。</summary>
    public string Kind { get; init; } = "";

    /// <summary>受け取ってよい canonical actionId。</summary>
    public IReadOnlyList<string> AllowedActionIds { get; init; } = [];

    /// <summary>最終更新時刻（UTC）。</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>変数の値。シークレットでは出さない。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; init; }

    /// <summary>シークレットが設定済みであること。変数では出さない。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ValueConfigured { get; init; }
}

/// <summary>Action バインディングの作成要求。</summary>
public sealed class CreateTenantActionBindingRequest
{
    /// <summary>名前。作成後は変えない。</summary>
    public string Name { get; set; } = "";

    /// <summary><c>variable</c> または <c>secret</c>。</summary>
    public string Kind { get; set; } = "";

    /// <summary>値。空白のみは不可。</summary>
    public string? Value { get; set; }

    /// <summary>canonical actionId。1〜8 個。</summary>
    public IReadOnlyList<string>? AllowedActionIds { get; set; }
}

/// <summary>Action バインディングの更新要求。名前は経路にある。</summary>
public sealed class UpdateTenantActionBindingRequest
{
    /// <summary>既存と同じ種別。変更は拒否する。</summary>
    public string Kind { get; set; } = "";

    /// <summary>変数では必須。シークレットで省略すると暗号文を維持する。</summary>
    public string? Value { get; set; }

    /// <summary>許可一覧の全体置換。</summary>
    public IReadOnlyList<string>? AllowedActionIds { get; set; }
}
