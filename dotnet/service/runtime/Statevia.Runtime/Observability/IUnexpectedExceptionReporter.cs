namespace Statevia.Runtime.Observability;

/// <summary>
/// ホストが想定外例外を任意のエラー監視へ送る出口。
/// </summary>
/// <remarks>
/// <para>DSN 未設定時の実装は no-op。Engine / Application からは呼ばない。</para>
/// <para>4xx・検証エラー・協調 Cancel は呼び出し側で除外する。</para>
/// </remarks>
public interface IUnexpectedExceptionReporter
{
    /// <summary>想定外例外を報告する。未有効時は何もしない。</summary>
    /// <param name="exception">捕捉した例外。</param>
    /// <param name="tags">相関タグ。欠けるキーは付けない。</param>
    void Report(Exception exception, UnexpectedExceptionTags tags);
}
