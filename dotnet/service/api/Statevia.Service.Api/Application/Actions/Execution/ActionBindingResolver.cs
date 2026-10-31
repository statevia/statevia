using Microsoft.EntityFrameworkCore;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>定義の参照名を、実行テナントの行へ解決する。</summary>
internal interface IActionBindingResolver
{
    /// <summary>参照があるときだけ解決し、リクエストの別フィールドへ載せる。</summary>
    /// <param name="request">未解決の参照を持つリクエスト。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>解決済みリクエスト。失敗時は Action 失敗。</returns>
    Task<(ActionExecutionRequest Request, ActionExecutionResult? Failure)> ResolveAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken);
}

/// <summary><see cref="IActionBindingResolver"/> の永続化実装。</summary>
/// <remarks>
/// <para>参照した名前だけを読む。復号は許可された canonical actionId のシークレットだけ。</para>
/// <para>Singleton で登録する。クエリフィルタを外してもテナント述語は残す。</para>
/// </remarks>
/// <param name="dbFactory">コア DbContext。</param>
/// <param name="secretProtector">シークレットの復元。変数は復号しない。</param>
internal sealed class ActionBindingResolver(
    IDbContextFactory<CoreDbContext> dbFactory,
    ITenantSecretProtector secretProtector) : IActionBindingResolver
{
    /// <inheritdoc />
    public async Task<(ActionExecutionRequest Request, ActionExecutionResult? Failure)> ResolveAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var references = request.BindingReferences;
        if (references is null || references.IsEmpty)
        {
            return (request, null);
        }

        if (!Guid.TryParse(request.TenantId, out var tenantId))
        {
            return (request, ActionBindingDispatch.Unavailable("Bindings could not be resolved."));
        }

        var names = references.Secrets.Values
            .Concat(references.Variables.Values)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.TenantActionBindings
            .IgnoreQueryFilters()
            .Include(row => row.AllowedActions)
            .Where(row => row.TenantId == tenantId && names.Contains(row.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byName = rows.ToDictionary(row => row.Name, StringComparer.Ordinal);

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (slot, referenceName) in references.Secrets)
        {
            var failure = TryTake(
                byName,
                slot,
                referenceName,
                TenantActionBindingKinds.Secret,
                request.ActionId,
                tenantId,
                resolved);
            if (failure is not null)
            {
                return (request, failure);
            }
        }

        foreach (var (slot, referenceName) in references.Variables)
        {
            var failure = TryTake(
                byName,
                slot,
                referenceName,
                TenantActionBindingKinds.Variable,
                request.ActionId,
                tenantId,
                resolved);
            if (failure is not null)
            {
                return (request, failure);
            }
        }

        return (request with { Bindings = resolved, BindingReferences = null }, null);
    }

    /// <summary>1 スロットを解決する。失敗メッセージに値は含めない。</summary>
    private ActionExecutionResult? TryTake(
        Dictionary<string, TenantActionBindingRow> byName,
        string slot,
        string referenceName,
        string expectedKind,
        string actionId,
        Guid tenantId,
        Dictionary<string, string> resolved)
    {
        if (!byName.TryGetValue(referenceName, out var row))
        {
            return ActionBindingDispatch.Unavailable($"Binding '{slot}' was not found.");
        }

        if (!string.Equals(row.Kind, expectedKind, StringComparison.Ordinal))
        {
            return ActionBindingDispatch.Unavailable($"Binding '{referenceName}' has a different kind.");
        }

        if (!row.AllowedActions.Any(allowed => string.Equals(allowed.ActionId, actionId, StringComparison.Ordinal)))
        {
            return ActionBindingDispatch.Unavailable($"Binding '{referenceName}' is not allowed for this action.");
        }

        if (expectedKind == TenantActionBindingKinds.Variable)
        {
            if (string.IsNullOrEmpty(row.ValuePlaintext))
            {
                return ActionBindingDispatch.Unavailable($"Binding '{referenceName}' could not be restored.");
            }

            resolved[slot] = row.ValuePlaintext;
            return null;
        }

        if (string.IsNullOrEmpty(row.ValueProtected))
        {
            return ActionBindingDispatch.Unavailable($"Binding '{referenceName}' could not be restored.");
        }

        try
        {
            resolved[slot] = secretProtector.Unprotect(
                tenantId,
                TenantActionBindingPurpose.For(row.BindingId),
                row.ValueProtected);
        }
        catch (InvalidOperationException)
        {
            return ActionBindingDispatch.Unavailable($"Binding '{referenceName}' could not be restored.");
        }

        return null;
    }
}

/// <summary>バインディング解決の失敗コードと、InProcess の拒否。</summary>
internal static class ActionBindingDispatch
{
    /// <summary>InProcess では参照を復号しない。</summary>
    public const string RejectedCode = "ActionBindingRejected";

    /// <summary>参照を解決できない。</summary>
    public const string UnavailableCode = "ActionBindingUnavailable";

    /// <summary>InProcess 拒否の固定文。値は含めない。</summary>
    public const string InProcessMessage = "Bindings are not available for InProcess execution.";

    /// <summary>参照がある InProcess 実行を、復号前に拒否する。</summary>
    /// <param name="request">実行リクエスト。</param>
    /// <returns>参照が無いときは null。</returns>
    public static ActionExecutionResult? RejectInProcess(ActionExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.BindingReferences is not { IsEmpty: false })
        {
            return null;
        }

        return Failure(RejectedCode, InProcessMessage);
    }

    /// <summary>OutOfProcess と Container で参照を解決する。参照が無ければそのまま返す。</summary>
    /// <param name="request">実行リクエスト。</param>
    /// <param name="resolver">解決器。未登録で参照があるときは失敗。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>解決済みリクエスト、または失敗。</returns>
    public static async Task<(ActionExecutionRequest Request, ActionExecutionResult? Failure)> ResolveIfNeededAsync(
        ActionExecutionRequest request,
        IActionBindingResolver? resolver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.BindingReferences is not { IsEmpty: false })
        {
            return (request, null);
        }

        if (resolver is null)
        {
            return (request, Unavailable("Bindings could not be resolved."));
        }

        return await resolver.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>解決できないときの Action 失敗。</summary>
    /// <param name="message">スロット名または参照名までの固定文。</param>
    /// <returns>失敗結果。</returns>
    public static ActionExecutionResult Unavailable(string message) =>
        Failure(UnavailableCode, message);

    private static ActionExecutionResult Failure(string code, string message) =>
        new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message,
        };
}

/// <summary>シークレット保護の用途。行 ID に結び、表示名は使わない。</summary>
internal static class TenantActionBindingPurpose
{
    /// <summary>保護 API の用途を返す。</summary>
    /// <param name="bindingId">行 ID。</param>
    /// <returns><c>action.binding.</c> に続けた N 形式の UUID。</returns>
    public static string For(Guid bindingId) => "action.binding." + bindingId.ToString("N");
}
