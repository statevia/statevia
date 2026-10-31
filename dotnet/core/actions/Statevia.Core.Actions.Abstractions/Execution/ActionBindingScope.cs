namespace Statevia.Core.Actions.Abstractions.Execution;

/// <summary>解決済みスロットを、その Action の 1 回の非同期文脈に置く。</summary>
/// <remarks>スコープ外は空。プロセス環境変数と workflow input には置かない。破棄で直前の文脈へ戻す。</remarks>
public static class ActionBindingScope
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, string>?> CurrentSlot = new();
    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>現在の実行文脈のスロット。スコープ外は空。</summary>
    public static IReadOnlyDictionary<string, string> Current => CurrentSlot.Value ?? Empty;

    /// <summary>スロットを現在の文脈に置き、破棄で元に戻す。</summary>
    /// <param name="bindings">この呼び出しのスロット。null は空。</param>
    /// <returns>破棄すると <see cref="Current"/> が戻るスコープ。</returns>
    public static IDisposable Begin(IReadOnlyDictionary<string, string>? bindings) =>
        new Scope(bindings ?? Empty);

    private sealed class Scope : IDisposable
    {
        private readonly IReadOnlyDictionary<string, string>? _previous;
        private bool _disposed;

        public Scope(IReadOnlyDictionary<string, string> bindings)
        {
            _previous = CurrentSlot.Value;
            CurrentSlot.Value = bindings;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CurrentSlot.Value = _previous;
        }
    }
}

/// <summary>定義が参照している、未解決のスロット名とテナント側の名前。</summary>
/// <param name="Secrets">スロット名 → シークレット名。</param>
/// <param name="Variables">スロット名 → 変数名。</param>
public sealed record ActionBindingReferences(
    IReadOnlyDictionary<string, string> Secrets,
    IReadOnlyDictionary<string, string> Variables)
{
    /// <summary>参照が無いか。</summary>
    public bool IsEmpty => Secrets.Count == 0 && Variables.Count == 0;
}
