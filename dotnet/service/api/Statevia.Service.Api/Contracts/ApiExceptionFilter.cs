using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Statevia.Runtime.Observability;
using Statevia.Service.Api.Application.Actions.Versioning;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// 例外から契約エラー（404 / 413 / 422 / 500）への写像を一箇所に集約するためのフィルター。
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;
    private readonly IUnexpectedExceptionReporter _unexpectedExceptions;
    private readonly ITenantContextAccessor _tenantContext;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="logger">ロガー。</param>
    /// <param name="unexpectedExceptions">想定外 500 の任意送信先。</param>
    /// <param name="tenantContext">解決済みテナント。未解決ならタグに載せない。</param>
    public ApiExceptionFilter(
        ILogger<ApiExceptionFilter> logger,
        IUnexpectedExceptionReporter unexpectedExceptions,
        ITenantContextAccessor tenantContext)
    {
        _logger = logger;
        _unexpectedExceptions = unexpectedExceptions;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// 未処理例外を <see cref="ExceptionContext.Result"/> に変換する。
    /// </summary>
    /// <remarks>
    /// 例外の連鎖（<see cref="Exception.InnerException"/>）を外側からたどり、最初に契約エラーへ写せた例外で応答を決める。
    /// 原因を内側に残した <see cref="ApiValidationException"/>（例: Engine の <see cref="InvalidOperationException"/> を包んだもの）を
    /// 内側の型で判定して 500 にしないため。どれにも当てはまらなければ 500。
    /// </remarks>
    /// <param name="context">例外コンテキスト。</param>
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ex = context.Exception;
        var result = EnumerateExceptionChain(ex)
            .Select(TryMapToContractError)
            .FirstOrDefault(mapped => mapped is not null)
            ?? LogInternalError(context.HttpContext, ex);

        context.Result = result;
        context.ExceptionHandled = true;
    }

    /// <summary>外側の例外から順に <see cref="Exception.InnerException"/> をたどる。</summary>
    private static IEnumerable<Exception> EnumerateExceptionChain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            yield return current;
    }

    /// <summary>契約エラーへ写せる例外なら応答を返す。写せなければ null。</summary>
    private static ObjectResult? TryMapToContractError(Exception exception) =>
        exception switch
        {
            ApiValidationException validation => ApiErrorResult.ValidationError(validation.Message, validation.Details),
            NotFoundException nf => ApiErrorResult.NotFound(nf.Message),
            UnauthorizedException unauthorized => ApiErrorResult.Unauthorized(unauthorized.Code, unauthorized.Message),
            ForbiddenException forbidden => ApiErrorResult.Forbidden(forbidden.Code, forbidden.Message),
            IdempotencyConflictException idem => ApiErrorResult.Conflict(ApiErrorCodes.IdempotencyKeyConflict, idem.Message),
            StateConflictException state => ApiErrorResult.Conflict(ApiErrorCodes.StateConflict, state.Message),
            DefinitionMigrationRequiredException migration => ApiErrorResult.ValidationError(
                ApiErrorCodes.DefinitionMigrationRequired,
                migration.Message),
            ModuleVersionResolutionException versionResolution => ApiErrorResult.ValidationError(
                ApiErrorCodes.ModuleVersionResolutionFailed,
                versionResolution.Message),
            ArgumentException arg => ApiErrorResult.ValidationError(arg.Message),
            // [RequestSizeLimit] 超過時に Kestrel がボディ読み込み（モデルバインド）で投げる。想定内のため 500 にしない。
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => ApiErrorResult.PayloadTooLarge(),
            _ => null
        };

    private ObjectResult LogInternalError(HttpContext httpContext, Exception exception)
    {
        _logger.UnhandledApiException(exception);
        _unexpectedExceptions.Report(exception, ReadTags(httpContext));
        return new ObjectResult(new ErrorResponse
        {
            Error = new ApiError
            {
                Code = ApiErrorCodes.InternalError,
                Message = "Internal server error"
            }
        })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
    }

    private UnexpectedExceptionTags ReadTags(HttpContext httpContext)
    {
        string? traceId = null;
        if (httpContext.Items.TryGetValue(RequestLogContext.TraceIdItemKey, out var traceObject)
            && traceObject is string resolvedTraceId
            && resolvedTraceId.Length > 0)
        {
            traceId = resolvedTraceId;
        }

        string? executionId = null;
        if (httpContext.Items.TryGetValue(RequestLogContext.ExecutionDisplayIdItemKey, out var executionObject)
            && executionObject is string resolvedExecutionId
            && resolvedExecutionId.Length > 0)
        {
            executionId = resolvedExecutionId;
        }

        return new UnexpectedExceptionTags(
            TraceId: traceId,
            TenantId: _tenantContext.TenantId,
            ExecutionId: executionId);
    }
}
