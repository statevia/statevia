using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Actions.Abstractions.Visibility;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.Engine;
using Statevia.Core.Engine.Execution;
using Statevia.Core.Engine.Infrastructure;
using Statevia.Core.Engine.Scheduler;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Application.Actions.Publication;
using Statevia.Service.Api.Application.Definition;
using Statevia.Service.Api.Hosting;
using ActionExecutionTestSupport = Statevia.Service.Api.Tests.Application.Actions.Execution.ActionExecutionTestSupport;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>
/// 退役した signal（<see cref="WellKnownActionIds.Signal"/>）の扱いを検証する。
/// 新しい定義では 422 相当の例外、保存済みの版は読み込めて、実行するとノードが失敗する。
/// </summary>
public sealed class RetiredSignalActionTests
{
    /// <summary>signal ノードを持つ Nodes 形式の定義（<c>error</c> の有無を選べる）。</summary>
    /// <param name="withErrorHandler">
    /// true なら signal ノードに <c>error: handler</c> を付け、成功時は endNode へ進む。
    /// false なら <c>error</c> を付けず、成功時の次を handler にする（handler を到達可能に保つため）。
    /// </param>
    private static string CreateSignalYaml(bool withErrorHandler) => $"""
        version: 1
        workflow:
          name: LegacySignal
        nodes:
          - name: start
            type: start
            next: emit
          - name: emit
            type: action
            action: statevia.action.builtin.execution.signal
            input:
              signal: approve
            next: {(withErrorHandler ? "endNode" : "handler")}
        {(withErrorHandler ? "    error: handler\n" : "")}  - name: handler
            type: action
            action: statevia.action.builtin.execution.noop
            next: endNode
          - name: endNode
            type: end
        """;

    /// <summary>signal を使う定義を新しく保存・検証すると、退役と代わりの手段を示す例外になる。</summary>
    [Fact]
    public void ValidateAndCompile_SignalAction_ThrowsRetiredWithReplacementHint()
    {
        // Arrange
        var svc = CreateSut(ActionExecutionTestSupport.CreateCatalogWithBuiltins());
        var yaml = CreateSignalYaml(withErrorHandler: true);

        // Act
        var ex = Assert.Throws<ArgumentException>(() => svc.ValidateAndCompile("LegacySignal", yaml));

        // Assert
        Assert.Contains("Action 'statevia.action.builtin.execution.signal' in state 'emit' is retired.", ex.Message, StringComparison.Ordinal);
        Assert.Contains("/resume", ex.Message, StringComparison.Ordinal);
        Assert.Contains("event.publish", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>signal を使う保存済みの版は、退役後の Catalog でも読み込める。</summary>
    [Fact]
    public void RestoreFromStoredVersion_SignalAction_Restores()
    {
        // Arrange
        var yaml = CreateSignalYaml(withErrorHandler: true);
        var compiledJson = CompileWithLegacyCatalog(yaml);
        var svc = CreateSut(ActionExecutionTestSupport.CreateCatalogWithBuiltins());

        // Act
        var restored = svc.RestoreFromStoredVersion(yaml, compiledJson);

        // Assert
        Assert.NotNull(restored.StateExecutorFactory.GetExecutor("emit"));
    }

    /// <summary>保存済みの版の signal ノードに error があれば、失敗して遷移先へ進み、実行は Completed で終わる。</summary>
    [Fact]
    public async Task Start_RestoredSignalWithErrorHandler_RunsHandlerAndCompletes()
    {
        // Arrange
        var yaml = CreateSignalYaml(withErrorHandler: true);
        var restored = CreateSut(ActionExecutionTestSupport.CreateCatalogWithBuiltins())
            .RestoreFromStoredVersion(yaml, CompileWithLegacyCatalog(yaml));
        var engine = CreateTestEngine();

        // Act
        var id = engine.Start(restored);
        var snapshot = await WaitForTerminalAsync(engine, id);

        // Assert
        Assert.True(snapshot.IsCompleted);
        Assert.False(snapshot.IsFailed);
        var graph = engine.ExportExecutionGraph(id);
        Assert.Contains("\"handler\"", graph, StringComparison.Ordinal);
    }

    /// <summary>保存済みの版の signal ノードに error が無ければ、実行は Failed で終わる。</summary>
    [Fact]
    public async Task Start_RestoredSignalWithoutErrorHandler_FailsExecution()
    {
        // Arrange
        var yaml = CreateSignalYaml(withErrorHandler: false);
        var restored = CreateSut(ActionExecutionTestSupport.CreateCatalogWithBuiltins())
            .RestoreFromStoredVersion(yaml, CompileWithLegacyCatalog(yaml));
        var engine = CreateTestEngine();

        // Act
        var id = engine.Start(restored);
        var snapshot = await WaitForTerminalAsync(engine, id);

        // Assert
        Assert.True(snapshot.IsFailed);
        Assert.False(snapshot.IsCompleted);
        Assert.False(snapshot.IsCancelled);
    }

    /// <summary>退役用の実行器は、入力値を含まない固定文の例外を投げる。</summary>
    [Fact]
    public async Task RetiredActionState_Execute_ThrowsWithoutInputValues()
    {
        // Arrange
        var state = new RetiredActionState(WellKnownActionIds.Signal, RetiredBuiltinActions.SignalReplacementHint);
        var input = new Dictionary<string, object?> { ["signal"] = "secret-signal-name" };

        // Act
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            state.ExecuteAsync(null!, input, CancellationToken.None));

        // Assert
        Assert.StartsWith("Action 'statevia.action.builtin.execution.signal' is retired.", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-signal-name", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 退役前の Catalog（signal を通常の Builtin として載せたもの）で YAML をコンパイルし、保存済みの版の compiled JSON を作る。
    /// </summary>
    /// <param name="yaml">定義 YAML。</param>
    /// <returns>保存済みの版に相当する compiled JSON。</returns>
    private static string CompileWithLegacyCatalog(string yaml)
    {
        var legacyCatalog = new InMemoryActionCatalog();
        legacyCatalog.Register(
            CreateBuiltinDescriptor(WellKnownActionIds.NoOpCanonical),
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new NoOpState())),
            BuiltinActionSchemas.NoOp(WellKnownActionIds.NoOpCanonical));
        legacyCatalog.Register(
            CreateBuiltinDescriptor(WellKnownActionIds.Signal),
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new NoOpState())),
            BuiltinActionSchemas.Signal(WellKnownActionIds.Signal));
        var (_, compiledJson) = CreateSut(legacyCatalog).ValidateAndCompile("LegacySignal", yaml);
        return compiledJson;
    }

    private static ActionDescriptor CreateBuiltinDescriptor(string actionId) =>
        new()
        {
            ActionId = actionId,
            ModuleId = "statevia.action.builtin",
            Version = "1.0.0",
            TrustLevel = ActionTrustLevel.Trusted,
            Source = ActionSourceKind.Builtin,
            Visibility = ActionVisibility.Builtin,
            ExecutionHints = new ActionExecutionHints { PreferredMode = ActionExecutionMode.InProcess },
        };

    private static DefinitionCompilerService CreateSut(IActionCatalog catalog)
    {
        var provider = ActionExecutionTestSupport.CreateProvider(catalog);
        return new DefinitionCompilerService(
            catalog,
            provider.GetRequiredService<IActionVisibilityResolver>(),
            new DefinitionLoadStrategy(new StateWorkflowDefinitionLoader(), new NodesWorkflowDefinitionLoader()),
            provider,
            NullLogger<DefinitionCompilerService>.Instance);
    }

    private static ExecutionEngine CreateTestEngine() =>
        new(
            new DefaultScheduler(1),
            new DefaultExecutionInstanceFactory(),
            new UuidV7ExecutionIdGenerator(),
            NullLoggerFactory.Instance);

    /// <summary>実行が終端（Completed / Failed / Cancelled）になるまで待つ。</summary>
    /// <param name="engine">実行中のエンジン。</param>
    /// <param name="executionId">実行 ID。</param>
    /// <returns>終端のスナップショット。</returns>
    private static async Task<ExecutionSnapshot> WaitForTerminalAsync(ExecutionEngine engine, string executionId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (engine.GetSnapshot(executionId) is { } snapshot
                && (snapshot.IsCompleted || snapshot.IsFailed || snapshot.IsCancelled))
            {
                return snapshot;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"Execution '{executionId}' did not reach a terminal state.");
    }
}
