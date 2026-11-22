namespace Statevia.Service.Api.Contracts.Admin;

/// <summary>GET /v1/admin/modules の 1 件。呼び出し元テナントの Module のロード結果。</summary>
/// <remarks>
/// テナント向けの契約なので、サーバーのファイル配置（entry DLL の絶対パスや modules ルート）を返さない。
/// 絶対パスは運用者向けのログ（EventId 60003 / 60004）にだけ残す。<see cref="Message"/> も例外の本文を含めない固定の文である。
/// </remarks>
public sealed record AdminModuleListItemDto
{
    /// <summary>Module ID。</summary>
    public required string ModuleId { get; init; }

    /// <summary>表示名。</summary>
    public required string Name { get; init; }

    /// <summary>バージョン。</summary>
    public required string Version { get; init; }

    /// <summary>load 状態（Loaded / Failed / Skipped / Duplicate）。</summary>
    public required string Status { get; init; }

    /// <summary>entry DLL の SHA256（hex）。</summary>
    public required string Sha256 { get; init; }

    /// <summary>発見元ラベル。</summary>
    public string? SourceLabel { get; init; }

    /// <summary>load 完了 UTC。</summary>
    public required DateTimeOffset LoadedAtUtc { get; init; }

    /// <summary>状態説明。ロード結果ごとの固定の文で、例外の本文やファイルのパスは含めない。</summary>
    public string? Message { get; init; }
}
