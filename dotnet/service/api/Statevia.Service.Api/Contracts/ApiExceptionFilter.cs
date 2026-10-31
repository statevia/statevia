using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Statevia.Runtime.Observability;
using Statevia.Service.Api.Application.Actions.Versioning;
using Statevia.Service.Api.Hosting;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// 例外から契約エラー（404 / 422 / 500）への写像を一箇所に集約するためのフィルター。
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
    /// <param name="context">例外コンテキスト。</param>
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ex = context.Exception;
        var root = ex;
        while (root.InnerException is { } inner)
            root = inner;

        var result = root switch
        {
            ApiValidationException validation => ApiErrorResult.ValidationError(validation.Message, validation.Details),
            NotFoundException nf => ApiErrorResult.NotFound(nf.Message),
            UnauthorizedException unauthorized => ApiErrorResult.Unauthorized(unauthorized.Code, unauthorized.Message),
            ForbiddenException forbidden => ApiErrorResult.Forbidden(forbidden.Code, forbidden.Message),
            IdempotencyConflictException idem => ApiErrorResult.Conflict("IDEMPOTENCY_KEY_CONFLICT", idem.Message),
            StateConflictException state => ApiErrorResult.Conflict("STATE_CONFLICT", state.Message),
            DefinitionMigrationRequiredException migration => ApiErrorResult.ValidationError(
                DefinitionMigrationRequiredException.ErrorCode,
                migration.Message),
            ModuleVersionResolutionException versionResolution => ApiErrorResult.ValidationError(
                "MODULE_VERSION_RESOLUTION_FAILED",
                versionResolution.Message),
            ArgumentException arg => ApiErrorResult.ValidationError(arg.Message),
            _ => LogInternalError(context.HttpContext, ex)
        };

        context.Result = result;
        context.ExceptionHandled = true;
    }

    private ObjectResult LogInternalError(HttpContext httpContext, Exception exception)
    {
        _logger.UnhandledApiException(exception);
        _unexpectedExceptions.Report(exception, ReadTags(httpContext));
        return new ObjectResult(new ErrorResponse
        {
            Error = new ApiError
            {
                Code = "INTERNAL_ERROR",
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
