using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary><see cref="NotificationSmtpConnectionLoader"/> がテナント行を接続へ写す範囲。</summary>
public sealed class NotificationSmtpConnectionLoaderTests
{
    private const string Password = "smtp-secret-value";

    /// <summary>行が無いテナントは固定文で失敗する。</summary>
    [Fact]
    public async Task LoadAsync_WhenRowMissing_ThrowsFixedMessage()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var sut = new NotificationSmtpConnectionLoader(database.Factory, new ScriptedProtector(Password));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.LoadAsync(TestTenantIds.DefaultTenantId, CancellationToken.None));

        // Assert
        Assert.Equal("Notification SMTP is not configured for the execution tenant.", exception.Message);
    }

    /// <summary>他テナントの行は読まず、指定テナントのパスワードだけを復元する。</summary>
    [Fact]
    public async Task LoadAsync_WhenRowExists_RestoresThatTenantOnly()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await InsertAsync(database, TestTenantIds.T1TenantId, "other.example.com", 25, "other", "other-secret", "other@example.com");
        await InsertAsync(database, TestTenantIds.DefaultTenantId, "smtp.example.com", 2525, "mailer", "protected", " from@example.com ");
        var sut = new NotificationSmtpConnectionLoader(database.Factory, new ScriptedProtector(Password));

        // Act
        var connection = await sut.LoadAsync(TestTenantIds.DefaultTenantId, CancellationToken.None);

        // Assert
        Assert.Equal(new NotificationSmtpDispatch("smtp.example.com", 2525, "mailer", Password, " from@example.com "), connection);
    }

    /// <summary>ユーザーが空の行はパスワードを復元せず、空白の既定 From は null にする。</summary>
    [Fact]
    public async Task LoadAsync_WhenUserEmpty_OmitsPasswordAndBlankFrom()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await InsertAsync(database, TestTenantIds.DefaultTenantId, "smtp.example.com", 587, "", "protected", "  ");
        var sut = new NotificationSmtpConnectionLoader(database.Factory, new ScriptedProtector(Password));

        // Act
        var connection = await sut.LoadAsync(TestTenantIds.DefaultTenantId, CancellationToken.None);

        // Assert
        Assert.Equal(new NotificationSmtpDispatch("smtp.example.com", 587, null, null, null), connection);
    }

    /// <summary>ユーザーがあるのに保護文字列が空なら復元失敗の固定文にする。</summary>
    [Fact]
    public async Task LoadAsync_WhenPasswordMissing_ThrowsRestoreFailed()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await InsertAsync(database, TestTenantIds.DefaultTenantId, "smtp.example.com", 587, "mailer", null, null);
        var sut = new NotificationSmtpConnectionLoader(database.Factory, new ScriptedProtector(Password));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.LoadAsync(TestTenantIds.DefaultTenantId, CancellationToken.None));

        // Assert
        Assert.Equal("Notification SMTP password could not be restored.", exception.Message);
        Assert.DoesNotContain(Password, exception.ToString(), StringComparison.Ordinal);
    }

    /// <summary>復元例外は固定文に置き、内側の文を残さない。</summary>
    [Fact]
    public async Task LoadAsync_WhenUnprotectFails_ThrowsRestoreFailedWithoutInnerMessage()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await InsertAsync(database, TestTenantIds.DefaultTenantId, "smtp.example.com", 587, "mailer", "protected", null);
        var sut = new NotificationSmtpConnectionLoader(database.Factory, new ScriptedProtector(Password, fail: true));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.LoadAsync(TestTenantIds.DefaultTenantId, CancellationToken.None));

        // Assert
        Assert.Equal("Notification SMTP password could not be restored.", exception.Message);
        Assert.DoesNotContain(Password, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("cipher", exception.ToString(), StringComparison.Ordinal);
    }

    private static async Task InsertAsync(
        SqliteTestDatabase database,
        Guid tenantId,
        string host,
        int port,
        string smtpUser,
        string? passwordProtected,
        string? defaultFrom)
    {
        await using var db = await database.Factory.CreateDbContextAsync();
        db.TenantNotificationSmtps.Add(new TenantNotificationSmtpRow
        {
            TenantId = tenantId,
            Host = host,
            Port = port,
            SmtpUser = smtpUser,
            PasswordProtected = passwordProtected,
            DefaultFrom = defaultFrom,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>テストが指定した平文、または復元失敗を返す。</summary>
    private sealed class ScriptedProtector(string plaintext, bool fail = false) : ITenantSecretProtector
    {
        /// <inheritdoc />
        public string Protect(Guid tenantId, string purpose, string value) => value;

        /// <inheritdoc />
        public string Unprotect(Guid tenantId, string purpose, string protectedPayload)
        {
            if (fail)
                throw new InvalidOperationException("cipher:" + plaintext);

            return plaintext;
        }
    }
}
