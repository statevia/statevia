using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>
/// <c>wait.subscribe</c> と同じ照合でトピックを発行する Event capability。
/// </summary>
/// <remarks>
/// <para>
/// 配送は <see cref="IEventIngressService"/>（HTTP <c>POST /v1/events</c> と同じ正本）。
/// Engine の <c>IEventProvider.PublishTopic</c> には渡さない。
/// </para>
/// <para>
/// input は <c>topic</c> 必須、<c>key</c> 任意（省略は空文字）、<c>payload</c> は受理するが再開値には使わない。
/// 一致 0 件でも <c>dispatched: true</c>。
/// </para>
/// <para>
/// 照合するのは、この実行を開始した Principal（定期実行は run-as、子のワークフローは親の開始者）が
/// 実行者以上で所属し、操作できる定義に入る実行の購読だけである。範囲の外の購読は黙って除く（0 件と同じ）。
/// 開始者が無効化・削除されていれば照合しない。<c>event.publish</c> を含む定義を開始して、開始者の権限より広い待機を再開させないため。
/// </para>
/// </remarks>
internal sealed class PublishActionState : IState<object?, object?>
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>スコープ付き ingress 解決用に構築する。</summary>
    /// <param name="scopeFactory">Worker 上で Application サービスを解決する factory。</param>
    public PublishActionState(IServiceScopeFactory scopeFactory) =>
        _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(StateContext ctx, object? input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (!ActionInputReader.TryReadObject(input, out var fields))
        {
            throw new ArgumentException("publish action requires input.topic.");
        }

        var topic = ActionInputReader.RequireString(fields, "topic");
        var key = ActionInputReader.OptionalString(fields, "key") ?? string.Empty;

        if (!Guid.TryParse(ctx.ExecutionId, out var publishingExecutionId) || publishingExecutionId == Guid.Empty)
        {
            throw new ArgumentException("publish action requires a publishing execution id.");
        }

        using var scope = _scopeFactory.CreateScope();
        var ingress = scope.ServiceProvider.GetRequiredService<IEventIngressService>();
        await ingress.PublishFromExecutionAsync(publishingExecutionId, topic, key, ct).ConfigureAwait(false);

        return new Dictionary<string, object?>
        {
            ["topic"] = topic,
            ["dispatched"] = true,
        };
    }
}
