using Statevia.Core.Actions.Abstractions.Execution;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>1 つの Action Host エンドポイントへの 1 回の RPC。</summary>
internal interface IActionHostCall
{
    /// <summary>Action をこのエンドポイントで実行する。</summary>
    /// <param name="request">実行リクエスト。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>実行結果。</returns>
    Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken);
}
