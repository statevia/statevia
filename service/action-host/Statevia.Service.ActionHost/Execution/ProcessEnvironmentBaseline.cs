namespace Statevia.Service.ActionHost.Execution;

/// <summary>待受開始時のプロセス環境。</summary>
/// <remarks>
/// <para>基準は <see cref="Capture"/> の最初の 1 回だけ固定する。その後の書き換えは基準に入らない。</para>
/// <para>復元は、基準に無い名前を削除し、基準にある名前を基準の値で上書きする。</para>
/// </remarks>
internal sealed class ProcessEnvironmentBaseline(IProcessEnvironment environment)
{
    private readonly object _captureGate = new();
    private readonly TaskCompletionSource _captured = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Dictionary<string, string>? _values;

    /// <summary>現在のプロセス環境を基準として固定する。2 回目以降は何もしない。</summary>
    public void Capture()
    {
        lock (_captureGate)
        {
            if (_values is not null)
            {
                return;
            }

            _values = new Dictionary<string, string>(environment.Read(), ProcessEnvironmentNames.Comparer);
        }

        _captured.TrySetResult();
    }

    /// <summary>基準が固定されるまで待つ。</summary>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>基準が固定されたとき完了するタスク。</returns>
    /// <exception cref="OperationCanceledException">待機中にキャンセルされたとき。</exception>
    public async Task WaitUntilCapturedAsync(CancellationToken cancellationToken) =>
        await _captured.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>プロセス環境を基準へ戻す。</summary>
    /// <exception cref="InvalidOperationException">基準を取る前に呼んだとき。</exception>
    public void Restore()
    {
        Dictionary<string, string> baseline;
        lock (_captureGate)
        {
            if (_values is null)
            {
                throw new InvalidOperationException("Process environment baseline is not captured.");
            }

            baseline = _values;
        }

        var current = new Dictionary<string, string>(environment.Read(), ProcessEnvironmentNames.Comparer);
        foreach (var name in current.Keys.Where(name => !baseline.ContainsKey(name)))
        {
            environment.Set(name, null);
        }

        foreach (var pair in baseline.Where(pair =>
            !current.TryGetValue(pair.Key, out var existing)
            || !string.Equals(existing, pair.Value, StringComparison.Ordinal)))
        {
            environment.Set(pair.Key, pair.Value);
        }
    }
}
