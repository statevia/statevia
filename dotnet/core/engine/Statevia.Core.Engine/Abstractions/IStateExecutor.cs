namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// IState の実行をエンジン向けにラップするインターフェース。
/// 状態名に応じた型なしで実行できるようにします。
/// </summary>
public interface IStateExecutor
{
    /// <summary>状態を非同期で実行し、出力を object として返します。</summary>
    /// <param name="ctx">状態の実行に使う文脈。</param>
    /// <param name="input">状態の入力。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>状態の出力。</returns>
    Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct);
}
