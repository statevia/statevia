using Statevia.Core.Engine.Infrastructure.Logging;

namespace Statevia.Service.Api.Hosting;

/// <summary>
/// ログ用スナップショットの簡易マスキング（共通ログマスキング導入前の妥協実装）。
/// </summary>
internal static class LogBodyRedactor
{
    /// <summary>
    /// 共通 `LogRedaction` へ委譲してマスク済み文字列を返す。
    /// </summary>
    /// <param name="text">マスクする文字列。</param>
    /// <param name="maxChars">残す最大文字数。</param>
    /// <returns>マスクし、上限で切り詰めた文字列。</returns>
    public static string Redact(string? text, int maxChars) => LogRedaction.Redact(text, maxChars);
}
