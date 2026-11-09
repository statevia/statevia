namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// 状態名から IStateExecutor を取得するファクトリ。
/// ワークフロー定義ごとに状態実装を登録します。
/// </summary>
public interface IStateExecutorFactory
{
    /// <summary>指定した状態名のエグゼキューターを取得します。未登録の場合は null。</summary>
    /// <param name="stateName">状態名。</param>
    /// <returns>状態の実行器。未登録なら <see langword="null"/>。</returns>
    IStateExecutor? GetExecutor(string stateName);
}
