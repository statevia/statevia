using Microsoft.Extensions.Options;

namespace Statevia.Infrastructure.Notification;

/// <summary><see cref="NotificationOptions.SmtpSettingsSource"/> に応じて <see cref="ISmtpConnectionSettingsProvider"/> を選択する。</summary>
internal sealed class SmtpConnectionSettingsProviderFactory : ISmtpConnectionSettingsProvider
{
    private readonly NotificationOptions _options;
    private readonly EnvironmentSmtpConnectionSettingsProvider _environmentProvider;
    private readonly DatabaseSmtpConnectionSettingsProvider _databaseProvider;
    private readonly KmsSmtpConnectionSettingsProvider _kmsProvider;

    /// <summary>各実装を注入する。</summary>
    /// <param name="options">通知の設定。<see cref="NotificationOptions.SmtpSettingsSource"/> で使う実装を選ぶ。</param>
    /// <param name="environmentProvider">環境変数と <see cref="NotificationOptions.Smtp"/> から SMTP 接続設定を読む実装。</param>
    /// <param name="databaseProvider">テナントの DB から読む実装（未実装のプレースホルダ）。</param>
    /// <param name="kmsProvider">KMS / Secret Manager から読む実装（未実装のプレースホルダ）。</param>
    public SmtpConnectionSettingsProviderFactory(
        IOptions<NotificationOptions> options,
        EnvironmentSmtpConnectionSettingsProvider environmentProvider,
        DatabaseSmtpConnectionSettingsProvider databaseProvider,
        KmsSmtpConnectionSettingsProvider kmsProvider)
    {
        _options = options.Value;
        _environmentProvider = environmentProvider;
        _databaseProvider = databaseProvider;
        _kmsProvider = kmsProvider;
    }

    /// <inheritdoc />
    public Task<SmtpConnectionSettings> GetAsync(SmtpConnectionSettingsRequest request, CancellationToken cancellationToken) =>
        Resolve().GetAsync(request, cancellationToken);

    private ISmtpConnectionSettingsProvider Resolve() =>
        _options.SmtpSettingsSource switch
        {
            NotificationSmtpSettingsSource.Environment => _environmentProvider,
            NotificationSmtpSettingsSource.Database => _databaseProvider,
            NotificationSmtpSettingsSource.KeyManagementService => _kmsProvider,
            _ => throw new InvalidOperationException($"Unsupported Notification SMTP settings source: {_options.SmtpSettingsSource}."),
        };
}
