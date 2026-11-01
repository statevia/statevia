using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>
/// 退役した Action の実行器。実行されると必ず例外を投げ、ノードを失敗させる。
/// </summary>
/// <remarks>
/// 保存済みの版に残った退役 Action のノードを、成功扱いで先へ進めないために使う。
/// 例外のメッセージは固定文で、ノードの入力値を含めない。
/// </remarks>
/// <param name="actionId">退役した Action の canonical ID。</param>
/// <param name="replacementHint">代わりに使う手段の説明（英語の固定文）。</param>
internal sealed class RetiredActionState(string actionId, string replacementHint) : IState<object?, Unit>
{
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">常に投げる。</exception>
    public Task<Unit> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct) =>
        throw new NotSupportedException(FormatMessage(actionId, replacementHint));

    /// <summary>退役を伝えるメッセージを組み立てる。</summary>
    /// <param name="actionId">退役した Action の canonical ID。</param>
    /// <param name="replacementHint">代わりに使う手段の説明。</param>
    /// <returns>英語の固定文。</returns>
    internal static string FormatMessage(string actionId, string replacementHint) =>
        $"Action '{actionId}' is retired. {replacementHint}";
}
