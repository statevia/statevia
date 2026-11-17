namespace Statevia.Core.Application.Contracts;

/// <summary>
/// HTTP のエラー応答の <c>error.code</c> に入る値の一覧。
/// </summary>
/// <remarks>
/// 利用者向けの正本は <c>docs/reference/error-codes.md</c>。値はクライアントが分岐に使う契約なので、変えたり消したりしない。
/// コードを足すときはここに定数を足し、同じ PR で <c>error-codes.md</c> の表にも足す（<c>Statevia.Architecture.Tests</c> の照合テストが両者の一致を確かめる）。
/// 定期実行の結果（<c>schedule_runs.error_code</c>）や Action の実行結果のコードは HTTP の応答に出ないので、ここには置かない。
/// </remarks>
public static class ApiErrorCodes
{
    /// <summary>401。認証できない（JWT / API キーが不正、Principal を解決できない、認証が無い）。</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>401。<c>X-Tenant-Id</c> のテナントが存在しない。</summary>
    public const string TenantUnresolved = "TENANT_UNRESOLVED";

    /// <summary>403。permission key が足りない。</summary>
    public const string PermissionDenied = "PERMISSION_DENIED";

    /// <summary>403。Project のロールが足りない。</summary>
    public const string ProjectAccessDenied = "PROJECT_ACCESS_DENIED";

    /// <summary>403。User / ServiceAccount の実行リソース許可に無い Project または定義を操作しようとした。</summary>
    public const string ResourceGrantDenied = "RESOURCE_GRANT_DENIED";

    /// <summary>403。テナント管理者が必要、テナントが Active でない、その他の拒否。</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>403。テナントが Suspended。</summary>
    public const string TenantSuspended = "TENANT_SUSPENDED";

    /// <summary>403。テナントが Archived。</summary>
    public const string TenantArchived = "TENANT_ARCHIVED";

    /// <summary>403。JWT のテナントと <c>X-Tenant-Id</c> が一致しない。</summary>
    public const string TenantHeaderMismatch = "TENANT_HEADER_MISMATCH";

    /// <summary>403。Principal が無効。</summary>
    public const string PrincipalInactive = "PRINCIPAL_INACTIVE";

    /// <summary>404。リソースが存在しない、または他テナントのため秘匿する。</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>409。同じ <c>X-Idempotency-Key</c> で本文が異なる。</summary>
    public const string IdempotencyKeyConflict = "IDEMPOTENCY_KEY_CONFLICT";

    /// <summary>409。状態が競合する（例: 削除されていない定義の復元）。</summary>
    public const string StateConflict = "STATE_CONFLICT";

    /// <summary>413。リクエストボディが上限を超えた。</summary>
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";

    /// <summary>422。入力の検証に失敗した。</summary>
    public const string ValidationError = "VALIDATION_ERROR";

    /// <summary>422。版共存のため、定義の再 compile か Module の版の配置が必要（Legacy 定義・ピン版が未ロードなど）。</summary>
    public const string DefinitionMigrationRequired = "DEFINITION_MIGRATION_REQUIRED";

    /// <summary>422。compile のときに Action Module の版を解決できない（alias や範囲が合わない）。</summary>
    public const string ModuleVersionResolutionFailed = "MODULE_VERSION_RESOLUTION_FAILED";

    /// <summary>500。想定外の失敗。詳細はクライアントに返さない。</summary>
    public const string InternalError = "INTERNAL_ERROR";
}
