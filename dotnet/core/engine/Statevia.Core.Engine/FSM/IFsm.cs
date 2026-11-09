namespace Statevia.Core.Engine.FSM;

/// <summary>
/// 事実駆動型 FSM のインターフェース。
/// (状態名, 事実) から遷移結果を O(1) で評価します。
/// </summary>
public interface IFsm
{
    /// <summary>指定した状態で指定した事実が発生したときの遷移結果を評価します。</summary>
    /// <param name="stateName">状態名。</param>
    /// <param name="fact">状態が返した Fact。</param>
    /// <returns>遷移の結果。遷移が無ければ <see cref="TransitionResult.None"/>。</returns>
    TransitionResult Evaluate(string stateName, string fact);
}
