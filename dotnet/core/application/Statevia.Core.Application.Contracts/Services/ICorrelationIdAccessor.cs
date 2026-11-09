namespace Statevia.Core.Application.Contracts.Services;

/// <summary>
/// 現在のリクエストコンテキストから相関 ID を取得する。HTTP 層の実装は TraceId 等を返す。
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>現在の相関 ID。取得不可の場合は空文字列。</summary>
    /// <returns>相関 ID。取得できなければ空文字。</returns>
    string GetCorrelationId();
}
