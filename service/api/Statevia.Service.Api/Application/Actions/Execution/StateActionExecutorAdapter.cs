using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary><see cref="IStateExecutor"/> から <see cref="IActionExecutor"/> への Adapter。</summary>
internal sealed class StateActionExecutorAdapter : IStateExecutor
{
    private readonly string _actionId;
    private readonly string? _resolvedModuleVersion;
    private readonly string _tenantId;
    private readonly IActionExecutor _actionExecutor;
    private readonly ActionBindingReferences? _bindings;

    /// <summary>
    /// Adapter を構築する。
    /// </summary>
    /// <param name="actionId">canonical actionId。</param>
    /// <param name="resolvedModuleVersion">compile 時にピンした Module 版（任意）。</param>
    /// <param name="tenantId"><c>tenants.tenant_id</c> UUID 文字列。</param>
    /// <param name="actionExecutor">Platform 実行ディスパッチャ。</param>
    /// <param name="bindings">状態の参照。空なら解決しない。</param>
    public StateActionExecutorAdapter(
        string actionId,
        string? resolvedModuleVersion,
        string tenantId,
        IActionExecutor actionExecutor,
        ActionBindingReferences? bindings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(actionExecutor);
        _actionId = actionId;
        _resolvedModuleVersion = string.IsNullOrWhiteSpace(resolvedModuleVersion) ? null : resolvedModuleVersion.Trim();
        _tenantId = tenantId;
        _actionExecutor = actionExecutor;
        _bindings = bindings is { IsEmpty: false } ? bindings : null;
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        var request = new ActionExecutionRequest
        {
            ExecutionId = ctx.ExecutionId,
            StateName = ctx.StateName,
            ActionId = _actionId,
            ResolvedModuleVersion = _resolvedModuleVersion,
            TenantId = _tenantId,
            BindingReferences = ToBindingReferences(_bindings),
        };

        var result = await _actionExecutor
            .ExecuteAsync(request, ctx, input, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? result.ErrorCode ?? "Action execution failed.");
        }

        return ActionExecutionRuntimeInputMapper.ToRuntimeOutput(result);
    }

    /// <summary>定義の参照を、解決前のリクエストフィールドへ写す。値はコピーしない。</summary>
    /// <param name="bindings">状態の参照。空なら null。</param>
    /// <returns>未解決の参照。</returns>
    private static ActionBindingReferences? ToBindingReferences(ActionBindingReferences? bindings)
    {
        if (bindings is null || bindings.IsEmpty)
        {
            return null;
        }

        return new ActionBindingReferences(bindings.Secrets, bindings.Variables);
    }
}
