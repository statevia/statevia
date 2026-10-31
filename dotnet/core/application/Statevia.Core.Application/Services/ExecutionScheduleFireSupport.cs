using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Scheduling;

namespace Statevia.Core.Application.Services;

/// <summary>スケジュール発火のログと、停滞判定に使う実効 Action タイムアウト。</summary>
/// <remarks>Dispatcher のコンストラクタ引数を増やさないための組。どちらもプロセス内で共有する。</remarks>
/// <param name="Logger">構造化ログ。</param>
/// <param name="ActionTimeout">停滞判定に使う実効 Action タイムアウト。</param>
internal sealed record ExecutionScheduleFireSupport(
    ILogger<ExecutionScheduleFireSupport> Logger,
    EffectiveActionTimeoutSettings ActionTimeout);
