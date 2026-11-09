using Microsoft.Extensions.Logging;

namespace Statevia.Core.Engine.Engine;

/// <summary><see cref="ExecutionEngine"/> のうち、実行ログを書き込む部分。ログの失敗は実行に伝えない。</summary>
public sealed partial class ExecutionEngine
{
    /// <summary>実行ログのメッセージ組み立てと <see cref="SafeLog"/> を一箇所に集約する。</summary>
    internal sealed class ExecutionEngineLogger
    {
        private readonly ILogger<ExecutionEngineLogger> _logger;

        /// <summary>新しいインスタンスを初期化する。</summary>
        /// <param name="logger">書き込み先のロガー。</param>
        public ExecutionEngineLogger(ILogger<ExecutionEngineLogger> logger)
        {
            _logger = logger;
        }

        /// <summary>StateContext 経由のログに Execution/State 文脈を付与するロガーを生成する。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="stateName">状態名。</param>
        /// <returns>実行 ID と状態名をスコープに付けるロガー。</returns>
        public StateContextLogger CreateStateContextLogger(string executionId, string stateName) =>
            new StateContextLogger(_logger, executionId, stateName);

        /// <summary>Execution の開始を記録する。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="definitionName">定義名。</param>
        /// <param name="initialState">開始する状態名。</param>
        public void LogExecutionStarted(string executionId, string definitionName, string initialState) =>
            SafeLog(() =>
                ExecutionLog.ExecutionStarted(_logger, executionId, definitionName, initialState));

        /// <summary>Execution のキャンセル要求を記録する。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        public void LogExecutionCancelRequested(string executionId) =>
            SafeLog(() =>
                ExecutionLog.ExecutionCancelRequested(_logger, executionId));

        /// <summary>Execution の実行ループの失敗を記録する。</summary>
        /// <param name="exception">記録する例外。</param>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="definitionName">定義名。</param>
        public void LogExecutionRunFailed(Exception exception, string executionId, string definitionName) =>
            SafeLog(() =>
                ExecutionLog.ExecutionRunFailed(_logger, exception, executionId, definitionName));

        /// <summary>状態の実行を予定したことを記録する。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="nodeId">ノード ID。</param>
        public void LogStateScheduled(string executionId, string stateName, string nodeId) =>
            SafeLog(() =>
                ExecutionLog.StateScheduled(_logger, executionId, stateName, nodeId));

        /// <summary>Join の状態の実行を予定したことを記録する。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="joinStateName">Join の状態名。</param>
        /// <param name="nodeId">ノード ID。</param>
        public void LogJoinStateScheduled(string executionId, string joinStateName, string nodeId) =>
            SafeLog(() =>
                ExecutionLog.JoinStateScheduled(_logger, executionId, joinStateName, nodeId));

        /// <summary>状態の完了を記録する。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="nodeId">ノード ID。</param>
        /// <param name="fact">状態が返した Fact。</param>
        /// <param name="elapsedMs">状態の実行にかかった時間（ミリ秒）。</param>
        public void LogStateCompleted(string executionId, string stateName, string nodeId, string fact, long elapsedMs) =>
            SafeLog(() =>
                ExecutionLog.StateCompleted(_logger, executionId, stateName, nodeId, fact, elapsedMs));

        /// <summary>Join の状態の完了を記録する。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="nodeId">ノード ID。</param>
        /// <param name="fact">状態が返した Fact。</param>
        public void LogJoinStateCompleted(string executionId, string stateName, string nodeId, string fact) =>
            SafeLog(() =>
                ExecutionLog.JoinStateCompleted(_logger, executionId, stateName, nodeId, fact));

        /// <summary>状態の実行の失敗を記録する。</summary>
        /// <param name="exception">記録する例外。</param>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="nodeId">ノード ID。</param>
        /// <param name="errorType">例外の型名。</param>
        public void LogStateExecuteFailed(Exception exception, string executionId, string stateName, string nodeId, string errorType) =>
            SafeLog(() =>
                ExecutionLog.StateExecuteFailed(_logger, exception, executionId, stateName, nodeId, errorType));

        /// <summary>Execution の完了を記録する。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="definitionName">定義名。</param>
        public void LogExecutionCompleted(string executionId, string definitionName) =>
            SafeLog(() =>
                ExecutionLog.ExecutionCompleted(_logger, executionId, definitionName));

        /// <summary>Fact=Failed による実行終端（運用者が対応すべき想定外）。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="definitionName">定義名。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="fact">状態が返した Fact。</param>
        public void LogExecutionTerminalFailure(string executionId, string definitionName, string stateName, string fact) =>
            SafeLog(() =>
                ExecutionLog.ExecutionTerminalFailure(_logger, executionId, definitionName, stateName, fact));

        /// <summary>Fact=Cancelled による実行終端。エンジンは Cancel 理由を区別しないため Information。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="definitionName">定義名。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="fact">状態が返した Fact。</param>
        public void LogExecutionCancelled(string executionId, string definitionName, string stateName, string fact) =>
            SafeLog(() =>
                ExecutionLog.ExecutionCancelled(_logger, executionId, definitionName, stateName, fact));

        /// <summary>state input のフォールバック等、継続可能だが入力品質に注意が必要な状況（入力評価警告）。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="inputKey">input のキー。</param>
        /// <param name="reason">警告の理由。</param>
        public void LogWarningInputEvaluation(string executionId, string stateName, string inputKey, string reason) =>
            SafeLog(() =>
                ExecutionLog.InputEvaluationWarning(_logger, executionId, stateName, inputKey, reason));

        /// <summary>条件遷移の path 解決警告（パス未解決・不正経路など）。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="fact">状態が返した Fact。</param>
        /// <param name="path">解決しようとした条件遷移の path。</param>
        /// <param name="reason">警告の理由。</param>
        public void LogWarningConditionPathResolution(string executionId, string stateName, string fact, string path, string reason) =>
            SafeLog(() =>
                ExecutionLog.ConditionPathResolutionWarning(_logger, executionId, stateName, fact, path, reason));

        /// <summary>FSM に次遷移がなく終端でもない停滞（遷移なし警告）。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="fact">状態が返した Fact。</param>
        public void LogWarningNoTransition(string executionId, string stateName, string fact) =>
            SafeLog(() =>
                ExecutionLog.NoTransition(_logger, executionId, stateName, fact));

        /// <summary>Fork 展開ハンドラの失敗。実行は Failed にせず、Engine ループは継続する。</summary>
        /// <param name="exception">記録する例外。</param>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        public void LogForkExpansionHandlerFailed(Exception exception, string executionId) =>
            SafeLog(() =>
                ExecutionLog.ForkExpansionHandlerFailed(_logger, exception, executionId));

        /// <summary>ノード完了通知ハンドラの実行失敗（エンジン進行は継続）。</summary>
        /// <param name="exception">記録する例外。</param>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        public void LogWarningNodeCompletedHandlerFailed(Exception exception, string executionId) =>
            SafeLog(() =>
                ExecutionLog.NodeCompletedHandlerFailed(_logger, exception, executionId));

        /// <summary>協調 Cancel 後に Completed でも次遷移しない。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="definitionName">定義名。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="fact">状態が返した Fact。</param>
        public void LogExecutionCancelSuppressedTransition(
            string executionId,
            string definitionName,
            string stateName,
            string fact) =>
            SafeLog(() =>
                ExecutionLog.ExecutionCancelSuppressedTransition(
                    _logger,
                    executionId,
                    definitionName,
                    stateName,
                    fact));

        /// <summary>終端失敗の後に、並行していた状態の事実で次遷移しない。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="definitionName">定義名。</param>
        /// <param name="stateName">状態名。</param>
        /// <param name="fact">状態が返した Fact。</param>
        public void LogExecutionFailureSuppressedTransition(
            string executionId,
            string definitionName,
            string stateName,
            string fact) =>
            SafeLog(() =>
                ExecutionLog.ExecutionFailureSuppressedTransition(
                    _logger,
                    executionId,
                    definitionName,
                    stateName,
                    fact));

        /// <summary>Engine 単体の Fork に対になる Join が無い（検証を通らない定義）。実行は終端失敗にする。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="definitionName">定義名。</param>
        /// <param name="stateName">状態名。</param>
        public void LogForkWithoutPairedJoin(string executionId, string definitionName, string stateName) =>
            SafeLog(() =>
                ExecutionLog.ForkWithoutPairedJoin(_logger, executionId, definitionName, stateName));

        /// <summary>Engine 単体で、枝の範囲の外、または範囲と別の Join に入った（検証を通らない定義）。実行は終端失敗にする。</summary>
        /// <param name="executionId">実行 ID（Engine 内のキー）。</param>
        /// <param name="definitionName">定義名。</param>
        /// <param name="joinState">到達した Join の状態名。</param>
        /// <param name="expectedJoinState">枝に対応する Join の状態名。対応する Join が無ければ null。</param>
        public void LogJoinReachedOutsideBranch(
            string executionId,
            string definitionName,
            string joinState,
            string? expectedJoinState) =>
            SafeLog(() =>
                ExecutionLog.JoinReachedOutsideBranch(_logger, executionId, definitionName, joinState, expectedJoinState));

        /// <summary>
        /// Wait が完了したが遷移先が決まらない（再開イベントが分からない、または <c>wait.events</c> に無い）ことを記録する。
        /// </summary>
        /// <param name="executionId">実行 ID。</param>
        /// <param name="definitionName">定義名。</param>
        /// <param name="stateName">Wait の状態名。</param>
        /// <param name="nodeId">Wait のノード ID。</param>
        /// <param name="eventName">引いたイベント名。分からなければ null。</param>
        public void LogWaitRouteUnresolved(
            string executionId,
            string definitionName,
            string stateName,
            string nodeId,
            string? eventName) =>
            SafeLog(() =>
                ExecutionLog.WaitRouteUnresolved(_logger, executionId, definitionName, stateName, nodeId, eventName));

        /// <summary>
        /// 遷移した状態の実行器が状態実行器ファクトリに無いことを記録する。実行は終端失敗にする（定義と登録の食い違い）。
        /// </summary>
        /// <param name="executionId">実行 ID。</param>
        /// <param name="definitionName">定義名。</param>
        /// <param name="stateName">実行器が無かった状態名。</param>
        public void LogExecutorNotFound(string executionId, string definitionName, string stateName) =>
            SafeLog(() =>
                ExecutionLog.ExecutorNotFound(_logger, executionId, definitionName, stateName));
    }

    /// <summary>
    /// 各ログ呼び出しに ExecutionId / StateName のスコープを付与する。
    /// </summary>
    internal sealed class StateContextLogger : ILogger
    {
        private readonly ILogger _logger;
        private readonly IReadOnlyDictionary<string, object?> _scope;

        /// <summary>新しいインスタンスを初期化する。</summary>
        /// <param name="logger">書き込み先のロガー。</param>
        /// <param name="executionId">スコープに付ける実行 ID。</param>
        /// <param name="stateName">スコープに付ける状態名。</param>
        public StateContextLogger(ILogger logger, string executionId, string stateName)
        {
            _logger = logger;
            _scope = new Dictionary<string, object?>
            {
                ["ExecutionId"] = executionId,
                ["StateName"] = stateName
            };
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state)!;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            using var _ = _logger.BeginScope(_scope);
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    /// <summary>ログ プロバイダが例外を投げても実行遷移を壊さない（ログ失敗を遷移に伝播させない）。</summary>
#pragma warning disable CA1031 // あらゆるログ実装からの例外を握りつぶす仕様
    private static void SafeLog(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // intentional: observability must not fail the engine
        }
    }
#pragma warning restore CA1031
}
