using System.Text.Json;

namespace Statevia.Core.Actions.Abstractions.Execution;

/// <summary>Platform 実行層へ渡す Action 実行リクエスト。</summary>
public sealed record ActionExecutionRequest
{
    /// <summary>実行インスタンス ID。</summary>
    public required string ExecutionId { get; init; }

    /// <summary>状態名。</summary>
    public required string StateName { get; init; }

    /// <summary>canonical actionId。</summary>
    public required string ActionId { get; init; }

    /// <summary>
    /// compile 時にピンした Module fullVersion。Module action の exact lookup に使用する。
    /// 未指定時は論理 actionId 経由の Legacy lookup（単一版のみ）を行う。
    /// </summary>
    public string? ResolvedModuleVersion { get; init; }

    /// <summary><c>tenants.tenant_id</c> UUID 文字列。</summary>
    public required string TenantId { get; init; }

    /// <summary>入力（OutOfProcess 向け。InProcess は StateContext 経由でも可）。</summary>
    public JsonElement? Input { get; init; }

    /// <summary><c>notification.send</c> の 1 回分の SMTP 接続。他 Action では null。input には入れない。</summary>
    public NotificationSmtpDispatch? NotificationSmtp { get; init; }

    /// <summary>解決済みスロット（スロット名 → 値）。input と SMTP フィールドには入れない。未設定は null。</summary>
    public IReadOnlyDictionary<string, string>? Bindings { get; init; }

    /// <summary>未解決の参照。gRPC には出さない。実行モードが決まってから <see cref="Bindings"/> へ解決する。</summary>
    public ActionBindingReferences? BindingReferences { get; init; }

    /// <summary>相関 ID（任意）。</summary>
    public string? CorrelationId { get; init; }

    /// <summary>実行期限（任意）。</summary>
    public DateTimeOffset? Deadline { get; init; }
}
