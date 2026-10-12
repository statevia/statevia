namespace Statevia.Core.Actions.Abstractions.Execution;

/// <summary>1 回の <c>notification.send</c> に渡す SMTP 接続。workflow input には入れない。</summary>
/// <param name="Host">SMTP ホスト。</param>
/// <param name="Port">SMTP ポート。</param>
/// <param name="User">SMTP ユーザー。空なら null。</param>
/// <param name="Password">復元したパスワード。ユーザーが空なら null。</param>
/// <param name="DefaultFrom">テナントの既定 From。空なら null。</param>
public sealed record NotificationSmtpDispatch(
    string Host,
    int Port,
    string? User,
    string? Password,
    string? DefaultFrom)
{
    /// <summary>この接続を使う canonical actionId。</summary>
    public const string ActionId = "statevia.action.reference.notification.send";

    /// <summary>指定 Action のときだけ呼び出しスコープを開く。他 Action では null。</summary>
    /// <param name="request">実行リクエスト。</param>
    /// <returns>閉じると接続を読めなくなるスコープ。対象外なら null。</returns>
    public static IDisposable? BeginScopeForAction(ActionExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.ActionId, ActionId, StringComparison.Ordinal))
            return null;

        if (request.NotificationSmtp is not { } connection)
            return null;

        return NotificationSmtpDispatchScope.Begin(connection);
    }
}

/// <summary>実行中の非同期文脈だけから SMTP 接続を読む。</summary>
public static class NotificationSmtpDispatchScope
{
    private static readonly AsyncLocal<NotificationSmtpDispatch?> CurrentSlot = new();

    /// <summary>現在の実行文脈の接続。スコープ外は null。</summary>
    public static NotificationSmtpDispatch? Current => CurrentSlot.Value;

    /// <summary>接続を現在の文脈に置き、破棄で元に戻す。</summary>
    /// <param name="connection">この呼び出しの接続。</param>
    /// <returns>破棄すると <see cref="Current"/> が戻るスコープ。</returns>
    public static IDisposable Begin(NotificationSmtpDispatch connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new Scope(connection);
    }

    private sealed class Scope : IDisposable
    {
        private readonly NotificationSmtpDispatch? _previous;
        private bool _disposed;

        public Scope(NotificationSmtpDispatch connection)
        {
            _previous = CurrentSlot.Value;
            CurrentSlot.Value = connection;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CurrentSlot.Value = _previous;
        }
    }
}
