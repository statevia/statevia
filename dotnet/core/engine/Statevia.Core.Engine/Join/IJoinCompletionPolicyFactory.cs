namespace Statevia.Core.Engine.Join;

/// <summary>
/// Join 完了条件戦略を生成するファクトリー。
/// </summary>
public interface IJoinCompletionPolicyFactory
{
    /// <summary>
    /// Join 状態ごとの戦略を生成する。
    /// </summary>
    /// <param name="joinStateName">Join の状態名。</param>
    /// <param name="conditionKind">Join の完了条件の種別。</param>
    /// <param name="dependencies">Join が待つ依存状態名の一覧。</param>
    /// <returns>Join の完了ポリシー。</returns>
    IJoinCompletionPolicy Create(string joinStateName, JoinConditionKind conditionKind, IReadOnlyList<string> dependencies);
}
