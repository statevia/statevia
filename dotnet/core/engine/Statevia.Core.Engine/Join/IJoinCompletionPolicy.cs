namespace Statevia.Core.Engine.Join;

/// <summary>
/// Join 完了条件を判定する戦略インターフェース。
/// </summary>
public interface IJoinCompletionPolicy
{
    /// <summary>
    /// 観測結果 <paramref name="results"/> から、
    /// Join を進められるか判定する。
    /// </summary>
    /// <param name="results">依存する状態名 → 観測した結果。</param>
    /// <returns>Join を進められるなら <see langword="true"/>。</returns>
    bool IsSatisfied(IReadOnlyDictionary<string, JoinObservedState> results);
}
