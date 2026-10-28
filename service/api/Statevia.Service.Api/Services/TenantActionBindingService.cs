using System.Text;
using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Contracts.Validation;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Service.Api.Contracts.Admin;

namespace Statevia.Service.Api.Services;

/// <summary>自テナントの Action 変数とシークレットを読み書きする。</summary>
public interface ITenantActionBindingService
{
    /// <summary>自テナントの一覧を返す。シークレットの値は含めない。</summary>
    /// <param name="callerPrincipalId">呼び出し主体。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>名前順の一覧。</returns>
    Task<IReadOnlyList<TenantActionBindingDto>> ListAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken);

    /// <summary>変数またはシークレットを作成する。</summary>
    /// <param name="callerPrincipalId">呼び出し主体。</param>
    /// <param name="request">名前、種別、値、許可 actionId。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>作成した行。シークレットの値は含めない。</returns>
    Task<TenantActionBindingDto> CreateAsync(
        Guid callerPrincipalId,
        CreateTenantActionBindingRequest request,
        CancellationToken cancellationToken);

    /// <summary>許可一覧を置換する。シークレットの値を省略すると暗号文を維持する。</summary>
    /// <param name="callerPrincipalId">呼び出し主体。</param>
    /// <param name="name">経路の名前。変更しない。</param>
    /// <param name="request">種別、値、許可一覧。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>更新後の行。</returns>
    Task<TenantActionBindingDto> UpdateAsync(
        Guid callerPrincipalId,
        string name,
        UpdateTenantActionBindingRequest request,
        CancellationToken cancellationToken);

    /// <summary>名前の行を消す。行が無くても成功する。</summary>
    /// <param name="callerPrincipalId">呼び出し主体。</param>
    /// <param name="name">名前。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    Task DeleteAsync(Guid callerPrincipalId, string name, CancellationToken cancellationToken);
}

/// <summary>テナント Action バインディングの保存。シークレットは保護 API だけを使う。</summary>
/// <param name="dbFactory">テナントフィルタ付きの DbContext。</param>
/// <param name="tenantContext">呼び出しテナント。</param>
/// <param name="tenantAdminAuthorization">テナント管理者判定。</param>
/// <param name="secretProtector">シークレットの可逆保護。</param>
/// <param name="ids">永続主キー。</param>
/// <param name="logger">保存ログ。値と保護文字列は出さない。</param>
internal sealed class TenantActionBindingService(
    IDbContextFactory<CoreDbContext> dbFactory,
    ITenantContextAccessor tenantContext,
    ITenantAdminAuthorization tenantAdminAuthorization,
    ITenantSecretProtector secretProtector,
    IIdGenerator ids,
    ILogger<TenantActionBindingService> logger) : ITenantActionBindingService
{
    /// <summary>テナントあたりの合計上限。一覧を 1 クエリに収める。</summary>
    public const int MaxBindingsPerTenant = 64;

    /// <summary>1 行の許可 actionId 上限。</summary>
    public const int MaxAllowedActionIds = 8;

    /// <summary>値の UTF-8 上限。保護 API の平文上限と同じ。</summary>
    public const int MaxValueUtf8Bytes = 4096;

    private const string UnauthorizedCode = "UNAUTHORIZED";
    private const string ForbiddenCode = "FORBIDDEN";

    /// <inheritdoc />
    public async Task<IReadOnlyList<TenantActionBindingDto>> ListAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);
        var tenantId = RequireTenantId();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.TenantActionBindings
            .IgnoreQueryFilters()
            .Include(row => row.AllowedActions)
            .Where(row => row.TenantId == tenantId)
            .OrderBy(row => row.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDto).ToArray();
    }

    /// <inheritdoc />
    public async Task<TenantActionBindingDto> CreateAsync(
        Guid callerPrincipalId,
        CreateTenantActionBindingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);
        var tenantId = RequireTenantId();
        var normalized = NormalizeCreate(request);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var count = await db.TenantActionBindings
            .IgnoreQueryFilters()
            .CountAsync(row => row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (count >= MaxBindingsPerTenant)
        {
            throw new ArgumentException($"A tenant can store at most {MaxBindingsPerTenant} action bindings.");
        }

        var nameTaken = await db.TenantActionBindings
            .IgnoreQueryFilters()
            .AnyAsync(row => row.TenantId == tenantId && row.Name == normalized.Name, cancellationToken)
            .ConfigureAwait(false);
        if (nameTaken)
        {
            throw new ArgumentException("Action binding name already exists.");
        }

        var now = DateTime.UtcNow;
        var row = new TenantActionBindingRow
        {
            BindingId = ids.NewSequentialGuid(),
            TenantId = tenantId,
            Name = normalized.Name,
            CreatedAt = now,
            UpdatedAt = now,
        };
        ApplyValue(row, tenantId, normalized.Kind, normalized.Value, keepExistingSecret: false);
        ReplaceAllowed(row, normalized.AllowedActionIds);
        db.TenantActionBindings.Add(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.ActionBindingSaved(callerPrincipalId, normalized.Kind, secretValueReplaced: normalized.Kind == TenantActionBindingKinds.Secret);
        return ToDto(row);
    }

    /// <inheritdoc />
    public async Task<TenantActionBindingDto> UpdateAsync(
        Guid callerPrincipalId,
        string name,
        UpdateTenantActionBindingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);
        var tenantId = RequireTenantId();
        var bindingName = NormalizeName(name);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantActionBindings
            .IgnoreQueryFilters()
            .Include(candidate => candidate.AllowedActions)
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.Name == bindingName,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            throw new ArgumentException("Action binding was not found.");
        }

        var kind = NormalizeKind(request.Kind);
        if (!string.Equals(kind, row.Kind, StringComparison.Ordinal))
        {
            throw new ArgumentException("Action binding kind cannot be changed.");
        }

        var allowed = NormalizeAllowed(request.AllowedActionIds);
        var keepExistingSecret = kind == TenantActionBindingKinds.Secret && request.Value is null;
        if (!keepExistingSecret)
        {
            ApplyValue(row, tenantId, kind, NormalizeValue(request.Value), keepExistingSecret: false);
        }

        ReplaceAllowed(row, allowed);
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.ActionBindingSaved(callerPrincipalId, kind, secretValueReplaced: !keepExistingSecret && kind == TenantActionBindingKinds.Secret);
        return ToDto(row);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid callerPrincipalId, string name, CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);
        var tenantId = RequireTenantId();
        var bindingName = NormalizeName(name);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantActionBindings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.Name == bindingName,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        db.TenantActionBindings.Remove(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>作成要求を契約どおりに正規化する。失敗メッセージに値は出さない。</summary>
    private static NormalizedBinding NormalizeCreate(CreateTenantActionBindingRequest request)
    {
        var kind = NormalizeKind(request.Kind);
        return new NormalizedBinding(NormalizeName(request.Name), kind, NormalizeValue(request.Value), NormalizeAllowed(request.AllowedActionIds));
    }

    /// <summary>名前を識別子として読む。</summary>
    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        if (!IdentifierConstraints.IsValid(trimmed, IdentifierConstraints.EventNameMaxLength))
        {
            throw new ArgumentException("Action binding name must be an identifier of 1 to 64 characters.");
        }

        return trimmed;
    }

    /// <summary>種別を variable または secret に限る。</summary>
    private static string NormalizeKind(string? kind)
    {
        var trimmed = kind?.Trim() ?? "";
        if (trimmed is TenantActionBindingKinds.Variable or TenantActionBindingKinds.Secret)
        {
            return trimmed;
        }

        throw new ArgumentException("Action binding kind must be variable or secret.");
    }

    /// <summary>値の空白と UTF-8 長を見る。値そのものはメッセージに出さない。</summary>
    private static string NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) > MaxValueUtf8Bytes)
        {
            throw new ArgumentException($"Action binding value must be 1 to {MaxValueUtf8Bytes} UTF-8 bytes and not whitespace.");
        }

        return value;
    }

    /// <summary>
    /// 許可 actionId を 1〜8 個の canonical 形式に限る。
    /// 結果リストの容量は上限定数にし、要求の件数ではメモリを確保しない。
    /// </summary>
    private static List<string> NormalizeAllowed(IReadOnlyList<string>? actionIds)
    {
        if (actionIds is null || actionIds.Count is < 1 or > MaxAllowedActionIds)
        {
            throw new ArgumentException($"An action binding allows 1 to {MaxAllowedActionIds} action IDs.");
        }

        var normalized = new List<string>(MaxAllowedActionIds);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var actionId in actionIds)
        {
            var trimmed = actionId?.Trim() ?? "";
            if (!IdentifierConstraints.IsValid(trimmed, IdentifierConstraints.TopicMaxLength)
                || !trimmed.Contains('.', StringComparison.Ordinal))
            {
                throw new ArgumentException("Allowed action IDs must be canonical identifiers.");
            }

            if (!seen.Add(trimmed))
            {
                throw new ArgumentException("Allowed action IDs must be unique.");
            }

            normalized.Add(trimmed);
        }

        return normalized;
    }

    /// <summary>種別に応じて平文列か保護列の片方だけを書く。</summary>
    private void ApplyValue(
        TenantActionBindingRow row,
        Guid tenantId,
        string kind,
        string value,
        bool keepExistingSecret)
    {
        if (keepExistingSecret)
        {
            return;
        }

        if (kind == TenantActionBindingKinds.Variable)
        {
            row.SetVariable(value);
            return;
        }

        row.SetSecret(secretProtector.Protect(tenantId, TenantActionBindingPurpose.For(row.BindingId), value));
    }

    /// <summary>許可一覧を全体置換する。</summary>
    private static void ReplaceAllowed(TenantActionBindingRow row, IReadOnlyList<string> actionIds)
    {
        row.AllowedActions.Clear();
        foreach (var actionId in actionIds)
        {
            row.AllowedActions.Add(new TenantActionBindingActionRow
            {
                BindingId = row.BindingId,
                ActionId = actionId,
            });
        }
    }

    /// <summary>行を応答へ写す。シークレットの値は含めない。</summary>
    private static TenantActionBindingDto ToDto(TenantActionBindingRow row)
    {
        var isSecret = row.Kind == TenantActionBindingKinds.Secret;
        return new TenantActionBindingDto
        {
            Name = row.Name,
            Kind = row.Kind,
            AllowedActionIds = row.AllowedActions
                .Select(action => action.ActionId)
                .OrderBy(actionId => actionId, StringComparer.Ordinal)
                .ToArray(),
            UpdatedAt = row.UpdatedAt,
            Value = isSecret ? null : row.ValuePlaintext,
            ValueConfigured = isSecret,
        };
    }

    /// <summary>認証コンテキストのテナント ID を返す。</summary>
    private Guid RequireTenantId()
    {
        if (tenantContext.IsResolved && tenantContext.TenantId is { } tenantId)
        {
            return tenantId;
        }

        throw new UnauthorizedException("Authentication required.", UnauthorizedCode);
    }

    /// <summary>テナント管理者でなければ拒否する。</summary>
    private async Task EnsureTenantAdminAsync(Guid principalId, CancellationToken cancellationToken)
    {
        if (!await tenantAdminAuthorization.IsTenantAdminAsync(principalId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenException("Tenant administrator required.", ForbiddenCode);
        }
    }

    /// <summary>正規化済みの作成値。</summary>
    /// <param name="Name">名前。</param>
    /// <param name="Kind">種別。</param>
    /// <param name="Value">平文。</param>
    /// <param name="AllowedActionIds">許可一覧。</param>
    private readonly record struct NormalizedBinding(
        string Name,
        string Kind,
        string Value,
        IReadOnlyList<string> AllowedActionIds);
}
