using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>
/// <c>notification.send</c> の実行リクエストにだけ、テナント SMTP 接続を載せてから内側のディスパッチャへ渡す。
/// </summary>
/// <param name="inner">Catalog、認可、実行モードのディスパッチャ。</param>
/// <param name="smtpConnections">実行テナントの接続読取。未登録なら <c>notification.send</c> は失敗する。</param>
internal sealed class NotificationSmtpActionExecutor(
    IActionExecutor inner,
    INotificationSmtpConnectionLoader? smtpConnections) : IActionExecutor
{
    private const string MissingConnectionMessage = "Notification SMTP is not configured for the execution tenant.";

    /// <inheritdoc />
    public async Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        StateContext stateContext,
        object? runtimeInput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prepared = await PrepareRequestAsync(request, cancellationToken).ConfigureAwait(false);
        if (prepared.ErrorMessage is not null)
        {
            return new ActionExecutionResult
            {
                Success = false,
                ErrorCode = "ActionExecutionFailed",
                ErrorMessage = prepared.ErrorMessage,
            };
        }

        return await inner.ExecuteAsync(prepared.Request, stateContext, runtimeInput, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary><c>notification.send</c> のときだけ接続を載せる。失敗メッセージに秘密は含めない。</summary>
    /// <param name="request">元の実行リクエスト。</param>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>渡すリクエストと、載せられないときの固定文。</returns>
    private async Task<PreparedRequest> PrepareRequestAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.ActionId, NotificationSmtpDispatch.ActionId, StringComparison.Ordinal))
            return new PreparedRequest(request, null);

        if (smtpConnections is null || !Guid.TryParse(request.TenantId, out var tenantId))
            return new PreparedRequest(request, MissingConnectionMessage);

        try
        {
            var connection = await smtpConnections.LoadAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return new PreparedRequest(request with { NotificationSmtp = connection }, null);
        }
        catch (InvalidOperationException ex)
        {
            return new PreparedRequest(request, ex.Message);
        }
    }

    /// <summary>接続を足したあとのリクエスト。</summary>
    /// <param name="Request">内側へ渡すリクエスト。</param>
    /// <param name="ErrorMessage">失敗時の固定文。成功時は null。</param>
    private readonly record struct PreparedRequest(ActionExecutionRequest Request, string? ErrorMessage);
}
