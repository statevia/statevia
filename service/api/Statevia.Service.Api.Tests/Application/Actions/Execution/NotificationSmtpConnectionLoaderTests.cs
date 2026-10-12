using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Application.Actions.Execution;
using Statevia.Service.Api.Contracts.Admin;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Application.Actions.Execution;

/// <summary>実行テナントの SMTP 行を接続へ復元する。</summary>
public sealed class NotificationSmtpConnectionLoaderTests
{
    private const string Password = "smtp-secret-value-9f3a";

    /// <summary>行が無いテナントは固定文で失敗する。</summary>
    [Fact]
    public async Task LoadAsync_WhenRowMissing_ThrowsFixedMessage()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var sut = CreateLoader(database);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.LoadAsync(TestTenantIds.DefaultTenantId, CancellationToken.None));

        // Assert
        Assert.Equal("Notification SMTP is not configured for the execution tenant.", exception.Message);
    }

    /// <summary>保護済みパスワードを同じテナントと用途で復元し、空の既定 From は null にする。</summary>
    [Fact]
    public async Task LoadAsync_WhenRowExists_RestoresPassword()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var protector = CreateDevelopmentProtector();
        await InsertRowAsync(
            database,
            TestTenantIds.DefaultTenantId,
            "mailer",
            protector.Protect(TestTenantIds.DefaultTenantId, TenantNotificationSmtpDto.PasswordPurpose, Password),
            "  ");
        var sut = CreateLoader(database, protector);

        // Act
        var connection = await sut.LoadAsync(TestTenantIds.DefaultTenantId, CancellationToken.None);

        // Assert
        Assert.Equal("smtp.example.com", connection.Host);
        Assert.Equal(2525, connection.Port);
        Assert.Equal("mailer", connection.User);
        Assert.Equal(Password, connection.Password);
        Assert.Null(connection.DefaultFrom);
    }

    /// <summary>ユーザーが空の行はパスワードを復元しない。</summary>
    [Fact]
    public async Task LoadAsync_WhenUserIsEmpty_ReturnsNullPassword()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await InsertRowAsync(database, TestTenantIds.DefaultTenantId, "", null, "from@example.com");
        var sut = CreateLoader(database);

        // Act
        var connection = await sut.LoadAsync(TestTenantIds.DefaultTenantId, CancellationToken.None);

        // Assert
        Assert.Null(connection.User);
        Assert.Null(connection.Password);
        Assert.Equal("from@example.com", connection.DefaultFrom);
    }

    /// <summary>ユーザーがあるのに保護文字列が空なら、理由を区別しない固定文で失敗する。</summary>
    [Fact]
    public async Task LoadAsync_WhenProtectedPasswordMissing_ThrowsFixedMessage()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await InsertRowAsync(database, TestTenantIds.DefaultTenantId, "mailer", null, null);
        var sut = CreateLoader(database);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.LoadAsync(TestTenantIds.DefaultTenantId, CancellationToken.None));

        // Assert
        Assert.Equal("Notification SMTP password could not be restored.", exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>復元できない保護文字列は、内側の例外を出さず固定文で失敗する。</summary>
    [Fact]
    public async Task LoadAsync_WhenUnprotectFails_ThrowsFixedMessage()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        await InsertRowAsync(database, TestTenantIds.DefaultTenantId, "mailer", "not-a-payload", null);
        var sut = CreateLoader(database);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.LoadAsync(TestTenantIds.DefaultTenantId, CancellationToken.None));

        // Assert
        Assert.Equal("Notification SMTP password could not be restored.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain(Password, exception.ToString(), StringComparison.Ordinal);
    }

    private static NotificationSmtpConnectionLoader CreateLoader(
        SqliteTestDatabase database,
        TenantSecretProtector? protector = null) =>
        new(database.Factory, protector ?? CreateDevelopmentProtector());

    private static async Task InsertRowAsync(
        SqliteTestDatabase database,
        Guid tenantId,
        string smtpUser,
        string? passwordProtected,
        string? defaultFrom)
    {
        await using var db = await database.Factory.CreateDbContextAsync();
        db.TenantNotificationSmtps.Add(new TenantNotificationSmtpRow
        {
            TenantId = tenantId,
            Host = "smtp.example.com",
            Port = 2525,
            SmtpUser = smtpUser,
            PasswordProtected = passwordProtected,
            DefaultFrom = defaultFrom,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static TenantSecretProtector CreateDevelopmentProtector() =>
        new(
            Options.Create(new SecretProtectionOptions()),
            new DevelopmentHostEnvironment(),
            new ConfigurationBuilder().Build());

    /// <summary>ローダーテスト用の Development ホスト。</summary>
    private sealed class DevelopmentHostEnvironment : IHostEnvironment
    {
        /// <inheritdoc />
        public string EnvironmentName { get; set; } = Environments.Development;

        /// <inheritdoc />
        public string ApplicationName { get; set; } = "Statevia.Service.Api.Tests";

        /// <inheritdoc />
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        /// <inheritdoc />
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
