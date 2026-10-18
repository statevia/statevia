using Statevia.Core.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>Action Host 経由の OutOfProcess 実行 Backend。</summary>
/// <remarks>Engine 内部状態（<c>StateContext</c>）は渡さず、リクエストと入力のみを Host へ委譲する。</remarks>
internal sealed class OutOfProcessBackend : IActionExecutionBackend
{
    private readonly IActionHostExecutionClient _client;
    private readonly IActionBindingResolver? _bindingResolver;

    /// <summary>OutOfProcess Backend を構築する。</summary>
    /// <param name="client">Action Host 実行クライアント。</param>
    /// <param name="bindingResolver">参照の解決。未登録で参照があるときは失敗する。</param>
    public OutOfProcessBackend(IActionHostExecutionClient client, IActionBindingResolver? bindingResolver = null)
    {
        _client = client;
        _bindingResolver = bindingResolver;
    }

    /// <inheritdoc />
    public ActionExecutionMode Mode => ActionExecutionMode.OutOfProcess;

    /// <inheritdoc />
    public string ProviderKey => "action-host";

    /// <inheritdoc />
    public async Task<ActionExecutionResult> ExecuteAsync(
        ActionBackendInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var hostRequest = ActionExecutionRuntimeInputMapper.WithRuntimeInput(
            invocation.Request,
            invocation.RuntimeInput);
        var prepared = await ActionBindingDispatch
            .ResolveIfNeededAsync(hostRequest, _bindingResolver, cancellationToken)
            .ConfigureAwait(false);
        if (prepared.Failure is not null)
        {
            return prepared.Failure;
        }

        return await _client.ExecuteAsync(prepared.Request, cancellationToken).ConfigureAwait(false);
    }
}
