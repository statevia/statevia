using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Actions.Abstractions.Execution;

/// <summary>Action 実行ディスパッチャ（Catalog → Visibility → Policy → Backend）。</summary>
public interface IActionExecutor
{
    /// <summary>Action を実行する。</summary>
    /// <param name="request">実行リクエスト。</param>
    /// <param name="stateContext">Engine から渡される状態コンテキスト（InProcess で Backend へ伝播）。</param>
    /// <param name="runtimeInput">Engine が解決した状態入力（InProcess 用）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>実行結果。未登録の Action やテナントから見えない Action は、例外ではなく <see cref="ActionExecutionResult.Success"/> が <see langword="false"/> の結果で返す。</returns>
    Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        StateContext stateContext,
        object? runtimeInput,
        CancellationToken cancellationToken);
}
