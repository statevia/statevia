using Statevia.Service.ActionHost.Execution;

namespace Statevia.Service.ActionHost.Tests;

/// <summary>テスト用のプロセス環境。実プロセスは変更しない。</summary>
internal sealed class DictionaryProcessEnvironment : IProcessEnvironment
{
    private readonly Dictionary<string, string> _values = new(ProcessEnvironmentNames.Comparer);

    /// <summary>削除時に失敗させる。</summary>
    public bool ThrowOnRemove { get; set; }

    /// <summary>名前があるか。</summary>
    /// <param name="name">環境変数名。</param>
    /// <returns>あるとき true。</returns>
    public bool Contains(string name) => _values.ContainsKey(name);

    /// <summary>値が一致するか。不一致の理由には値を含めない。</summary>
    /// <param name="name">環境変数名。</param>
    /// <param name="value">期待する値。</param>
    /// <returns>一致するとき true。</returns>
    public bool ValueEquals(string name, string value) =>
        _values.TryGetValue(name, out var existing)
        && string.Equals(existing, value, StringComparison.Ordinal);

    /// <summary>基準取得のあとで名前を足す。</summary>
    /// <param name="name">環境変数名。</param>
    /// <param name="value">値。</param>
    public void Seed(string name, string value) => _values[name] = value;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Read() =>
        new Dictionary<string, string>(_values, _values.Comparer);

    /// <inheritdoc />
    public void Set(string name, string? value)
    {
        if (value is null)
        {
            if (ThrowOnRemove)
            {
                throw new InvalidOperationException("remove failed");
            }

            _values.Remove(name);
            return;
        }

        _values[name] = value;
    }
}
