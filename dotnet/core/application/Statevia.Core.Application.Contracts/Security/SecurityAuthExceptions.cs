namespace Statevia.Core.Application.Contracts.Security;

/// <summary>401 Unauthorized。</summary>
public sealed class UnauthorizedException : Exception
{
    /// <summary>新しいインスタンスを初期化する。</summary>
    public UnauthorizedException()
        : this("Unauthorized.")
    {
    }

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="message">クライアント向けメッセージ。</param>
    public UnauthorizedException(string message)
        : this(message, ApiErrorCodes.Unauthorized)
    {
    }

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="message">クライアント向けメッセージ。</param>
    /// <param name="code">エラーコード。</param>
    public UnauthorizedException(string message, string code)
        : base(message) => Code = code;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="message">クライアント向けメッセージ。</param>
    /// <param name="innerException">内部例外。</param>
    public UnauthorizedException(string message, Exception innerException)
        : base(message, innerException) => Code = ApiErrorCodes.Unauthorized;

    /// <summary>エラーコード。</summary>
    public string Code { get; }
}

/// <summary>403 Forbidden。</summary>
public sealed class ForbiddenException : Exception
{
    /// <summary>新しいインスタンスを初期化する。</summary>
    public ForbiddenException()
        : this("Forbidden.")
    {
    }

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="message">クライアント向けメッセージ。</param>
    public ForbiddenException(string message)
        : this(message, ApiErrorCodes.Forbidden)
    {
    }

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="message">クライアント向けメッセージ。</param>
    /// <param name="code">エラーコード。</param>
    public ForbiddenException(string message, string code)
        : base(message) => Code = code;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="message">クライアント向けメッセージ。</param>
    /// <param name="innerException">内部例外。</param>
    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException) => Code = ApiErrorCodes.Forbidden;

    /// <summary>エラーコード。</summary>
    public string Code { get; }
}
