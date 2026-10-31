using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Hosting;

/// <summary>
/// Scheduler ホストが DI グラフを構築するための <see cref="IExecutionEngine"/>。
/// </summary>
/// <remarks>enqueue までの Start 受理は Engine を呼ばない。呼ばれた場合は実行せず失敗する。</remarks>
internal sealed class SchedulerHostRejectedExecutionEngine : IExecutionEngine
{
    /// <inheritdoc />
    public string Start(
        CompiledWorkflowDefinition definition,
        string? executionId = null,
        object? input = null,
        string? initialState = null) =>
        throw Rejected(definition, executionId, input, initialState);

    /// <inheritdoc />
    public void ResumeWaitNode(string executionId, string nodeId, string eventName) =>
        throw Rejected(executionId, nodeId, eventName);

    /// <inheritdoc />
    public void PublishEvent(string executionId, string eventName) =>
        throw Rejected(executionId, eventName);

    /// <inheritdoc />
    public ApplyResult PublishEvent(string executionId, string eventName, Guid clientEventId) =>
        throw Rejected(executionId, eventName, clientEventId);

    /// <inheritdoc />
    public Task CancelAsync(string executionId) =>
        throw Rejected(executionId);

    /// <inheritdoc />
    public Task<ApplyResult> CancelAsync(string executionId, Guid clientEventId) =>
        throw Rejected(executionId, clientEventId);

    /// <inheritdoc />
    public ExecutionSnapshot? GetSnapshot(string executionId) =>
        throw Rejected(executionId);

    /// <inheritdoc />
    public string ExportExecutionGraph(string executionId) =>
        throw Rejected(executionId);

    /// <inheritdoc />
    public void SetNodeCompletedHandler(Func<string, Task>? handler) =>
        throw Rejected(handler);

    /// <inheritdoc />
    public void SetSuspendHandler(Func<string, string, Task>? handler) =>
        throw Rejected(handler);

    /// <inheritdoc />
    public void SetForkExpansionHandler(Func<ForkExpansionEvent, Task>? handler) =>
        throw Rejected(handler);

    /// <inheritdoc />
    public void CompletePhysicalJoin(
        string executionId,
        string joinStateName,
        IReadOnlyDictionary<string, object?> branchOutputs,
        IReadOnlyList<PhysicalJoinContextFragment>? contextMerges = null) =>
        throw Rejected(executionId, joinStateName, branchOutputs, contextMerges);

    /// <inheritdoc />
    public ExecutionRuntimeCheckpoint? ExportCheckpoint(string executionId) =>
        throw Rejected(executionId);

    /// <inheritdoc />
    public void ImportCheckpoint(CompiledWorkflowDefinition definition, ExecutionRuntimeCheckpoint checkpoint) =>
        throw Rejected(definition, checkpoint);

    /// <inheritdoc />
    public bool Unload(string executionId) =>
        throw Rejected(executionId);

    private static InvalidOperationException Rejected(params object?[] arguments)
    {
        _ = arguments;
        return new InvalidOperationException("Scheduler host does not run the execution engine.");
    }
}

/// <summary>
/// Scheduler ホストが定義復元のファクトリ構築に使う <see cref="IActionExecutor"/>。
/// </summary>
/// <remarks>Start の enqueue は Action を実行しない。呼ばれた場合は実行せず失敗する。</remarks>
internal sealed class SchedulerHostRejectedActionExecutor : IActionExecutor
{
    /// <inheritdoc />
    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        StateContext stateContext,
        object? runtimeInput,
        CancellationToken cancellationToken) =>
        throw Rejected(request, stateContext, runtimeInput, cancellationToken);

    private static InvalidOperationException Rejected(params object?[] arguments)
    {
        _ = arguments;
        return new InvalidOperationException("Scheduler host does not execute actions.");
    }
}
