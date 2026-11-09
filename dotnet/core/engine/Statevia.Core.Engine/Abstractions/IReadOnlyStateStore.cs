namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// 完了済み状態の出力を読み取り専用で参照するストア。
/// Join 状態が依存状態の出力を取得する際に使用します。
/// </summary>
public interface IReadOnlyStateStore
{
    /// <summary>指定した状態の出力を取得します。未完了の場合は false を返します。</summary>
    /// <param name="stateName">状態名。</param>
    /// <param name="output">状態の出力。</param>
    /// <returns>状態が完了していて出力があれば <see langword="true"/>。</returns>
    bool TryGetOutput(string stateName, out object? output);
}
