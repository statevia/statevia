namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// ワークフロー状態を表します。ユーザー定義のロジックを非同期で実行します。
/// エンジンはユーザーロジックに干渉せず、CancellationToken による協調的キャンセルのみを提供します。
/// </summary>
/// <typeparam name="TInput">状態の入力の型。</typeparam>
/// <typeparam name="TOutput">状態の出力の型。</typeparam>
public interface IState<in TInput, TOutput>
{
    /// <summary>状態を非同期で実行し、結果を返します。</summary>
    /// <param name="ctx">状態の実行に使う文脈。</param>
    /// <param name="input">状態の入力。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>状態の出力。</returns>
    Task<TOutput> ExecuteAsync(StateContext ctx, TInput input, CancellationToken ct);
}
