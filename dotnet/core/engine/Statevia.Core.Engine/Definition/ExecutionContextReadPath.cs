namespace Statevia.Core.Engine.Definition;

/// <summary>読み取りパスの構文判定の結果。</summary>
public enum ReadPathValidity
{
    /// <summary>受理できる（許可された CallPath、または SimpleJsonPath）。</summary>
    Valid,

    /// <summary><c>$.sys.…(</c> の形だが、許可された CallPath ではない。</summary>
    InvalidCallPath,

    /// <summary>SimpleJsonPath として不正。</summary>
    InvalidPath
}

/// <summary>
/// Execution Context を根とする読み取りパス（状態の <c>input</c> と条件遷移の <c>when.path</c>）の構文判定。
/// </summary>
/// <remarks>
/// <para>
/// 定義の Loader、Level1 検証、Action の input スキーマ検証が同じ判定を使う。
/// 実行時の評価（<see cref="ExecutionContextPathResolver"/>）が受け付ける形と一致させるため、判定はここに 1 つだけ置く。
/// </para>
/// <para><c>output</c>（<c>$.vars…</c> のみ）はこの判定を使わない。</para>
/// </remarks>
public static class ExecutionContextReadPath
{
    /// <summary>
    /// <paramref name="path"/> を読み取りパスとして判定する。
    /// </summary>
    /// <param name="path">パス式（<see cref="SimpleJsonPath.IsPathExpression"/> が true のもの）。</param>
    /// <returns>判定結果。</returns>
    public static ReadPathValidity Classify(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path switch
        {
            _ when SysPathCall.IsValidCallPath(path) => ReadPathValidity.Valid,
            _ when SysPathCall.IsCallPathCandidate(path) => ReadPathValidity.InvalidCallPath,
            _ when SimpleJsonPath.IsValid(path) => ReadPathValidity.Valid,
            _ => ReadPathValidity.InvalidPath
        };
    }

    /// <summary>
    /// <see cref="ReadPathValidity.InvalidCallPath"/> のエラーに添える、許可された CallPath の説明文を返す。
    /// </summary>
    /// <returns>許可例と文字列引数の制約を含む英語のメッセージ断片。</returns>
    public static string FormatAllowedCallPathsHint() => SysPathCall.FormatAllowedCallPathsHint();
}
