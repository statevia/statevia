namespace Statevia.Service.Api.Bootstrap;

/// <summary>CLI 引数読み取り。</summary>
internal static class CliArgReader
{
    /// <summary>フラグの次トークンを必須値として読む。</summary>
    /// <param name="args">コマンドライン引数。</param>
    /// <param name="index">フラグの位置。読んだ値の位置へ進める。</param>
    /// <param name="flag">エラーメッセージに出すフラグ名。</param>
    /// <returns>フラグの次のトークン。</returns>
    /// <exception cref="ArgumentException">フラグの後にトークンが無いとき。</exception>
    public static string RequireValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {flag}.");
        index++;
        return args[index];
    }
}
