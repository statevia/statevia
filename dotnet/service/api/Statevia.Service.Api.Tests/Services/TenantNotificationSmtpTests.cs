using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Statevia.Service.Api.Contracts.Admin;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>テナント通知 SMTP の保存、非返却、削除。</summary>
public sealed class TenantNotificationSmtpTests
{
    private const string Password = "smtp-secret-value-9f3a";

    /// <summary>管理者の PUT 後、GET は接続情報を返し、応答にパスワードが無い。</summary>
    [Fact]
    public async Task ReplaceAndGet_Admin_OmitsPassword()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "smtp-admin@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);

        // Act
        var saved = await service.ReplaceAsync(
            adminId,
            new PutTenantNotificationSmtpRequest
            {
                Host = " smtp.example.com ",
                Port = 587,
                User = "mailer",
                Password = Password,
                DefaultFrom = " from@example.com "
            },
            CancellationToken.None);
        var loaded = await service.GetAsync(adminId, CancellationToken.None);
        var json = JsonSerializer.Serialize(loaded);

        // Assert
        Assert.True(saved.Configured);
        Assert.Equal("smtp.example.com", loaded.Host);
        Assert.Equal(587, loaded.Port);
        Assert.Equal("mailer", loaded.User);
        Assert.Equal("from@example.com", loaded.DefaultFrom);
        Assert.True(loaded.PasswordConfigured);
        Assert.DoesNotContain(Password, json, StringComparison.Ordinal);
    }

    /// <summary>保存した保護文字列は同一テナントと用途でのみ復元でき、別テナントでは復元できない。</summary>
    [Fact]
    public async Task Replace_StoresProtectedPassword_ForSameTenantOnly()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "smtp-protect@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var protector = CreateDevelopmentProtector();
        var service = CreateService(database, tenantContext, adminId, protector);
        await service.ReplaceAsync(
            adminId,
            Request("smtp.example.com", 25, "mailer", Password),
            CancellationToken.None);

        await using var db = await database.Factory.CreateDbContextAsync();
        var row = await db.TenantNotificationSmtps.SingleAsync();

        // Act
        var restored = protector.Unprotect(row.TenantId, TenantNotificationSmtpDto.PasswordPurpose, row.PasswordProtected!);
        var otherTenant = Assert.Throws<InvalidOperationException>(() =>
            protector.Unprotect(Guid.NewGuid(), TenantNotificationSmtpDto.PasswordPurpose, row.PasswordProtected!));

        // Assert
        Assert.Equal(Password, restored);
        Assert.Equal(TenantSecretProtector.UnwrapFailedMessage, otherTenant.Message);
        Assert.DoesNotContain(Password, otherTenant.ToString(), StringComparison.Ordinal);
    }

    /// <summary>ユーザーが空でパスワードがある置換は拒否し、行を作らない。</summary>
    [Fact]
    public async Task Replace_WhenUserEmptyAndPasswordSet_ThrowsAndStoresNothing()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "smtp-empty-user@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReplaceAsync(
                adminId,
                Request("smtp.example.com", 25, "  ", Password),
                CancellationToken.None));

        await using var db = await database.Factory.CreateDbContextAsync();
        var count = await db.TenantNotificationSmtps.CountAsync();

        // Assert
        Assert.Equal("request", exception.ParamName);
        Assert.DoesNotContain(Password, exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, count);
    }

    /// <summary>非管理者は参照できない。</summary>
    [Fact]
    public async Task Get_NonAdmin_ThrowsForbidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var memberId = await SecurityTestSeed.SeedUserAsync(database, "smtp-member@example.com", "password", isTenantAdmin: false);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, memberId);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetAsync(memberId, CancellationToken.None));
    }

    /// <summary>未設定の GET は 200 相当の未設定応答になる。</summary>
    [Fact]
    public async Task Get_WhenUnset_ReturnsUnconfigured()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "smtp-unset@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);

        // Act
        var loaded = await service.GetAsync(adminId, CancellationToken.None);

        // Assert
        Assert.False(loaded.Configured);
        Assert.Null(loaded.Host);
        Assert.Null(loaded.Port);
        Assert.Null(loaded.User);
        Assert.Null(loaded.DefaultFrom);
        Assert.False(loaded.PasswordConfigured);
    }

    /// <summary>DELETE のあと GET は未設定になる。</summary>
    [Fact]
    public async Task Delete_WhenPresent_ThenGetIsUnconfigured()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "smtp-delete@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);
        await service.ReplaceAsync(
            adminId,
            Request("smtp.example.com", 25, "", ""),
            CancellationToken.None);

        // Act
        await service.DeleteAsync(adminId, CancellationToken.None);
        var loaded = await service.GetAsync(adminId, CancellationToken.None);

        // Assert
        Assert.False(loaded.Configured);
        Assert.False(loaded.PasswordConfigured);
    }

    /// <summary>行が無い DELETE は成功する。</summary>
    [Fact]
    public async Task Delete_WhenAbsent_Completes()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "smtp-delete-absent@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var service = CreateService(database, tenantContext, adminId);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.DeleteAsync(adminId, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>再 PUT は同じ行を置換し、旧パスワードの保護文字列を残さない。</summary>
    [Fact]
    public async Task Replace_WhenAlreadyConfigured_ReplacesProtectedPassword()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var adminId = await SecurityTestSeed.SeedUserAsync(database, "smtp-replace@example.com", "password", isTenantAdmin: true);
        var tenantContext = new SettableTenantContextAccessor();
        var protector = CreateDevelopmentProtector();
        var service = CreateService(database, tenantContext, adminId, protector);
        await service.ReplaceAsync(
            adminId,
            Request("smtp.example.com", 25, "mailer", Password),
            CancellationToken.None);
        const string nextPassword = "next-smtp-secret-value";

        // Act
        await service.ReplaceAsync(
            adminId,
            Request("smtp.example.com", 2525, "mailer", nextPassword),
            CancellationToken.None);
        await using var db = await database.Factory.CreateDbContextAsync();
        var rows = await db.TenantNotificationSmtps.ToListAsync();
        var restored = protector.Unprotect(rows[0].TenantId, TenantNotificationSmtpDto.PasswordPurpose, rows[0].PasswordProtected!);

        // Assert
        Assert.Single(rows);
        Assert.Equal(2525, rows[0].Port);
        Assert.Equal(nextPassword, restored);
    }

    private static PutTenantNotificationSmtpRequest Request(string host, int port, string user, string password) =>
        new()
        {
            Host = host,
            Port = port,
            User = user,
            Password = password
        };

    private static TenantNotificationSmtpService CreateService(
        SqliteTestDatabase database,
        SettableTenantContextAccessor tenantContext,
        Guid callerPrincipalId,
        TenantSecretProtector? protector = null)
    {
        tenantContext.Set(TestTenantIds.DefaultContext with { PrincipalId = callerPrincipalId });
        return new TenantNotificationSmtpService(
            database.Factory,
            tenantContext,
            new TenantAdminAuthorization(new PlatformDataAccess(database.Factory, new DefaultIdGenerator())),
            protector ?? CreateDevelopmentProtector(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantNotificationSmtpService>.Instance);
    }

    private static TenantSecretProtector CreateDevelopmentProtector() =>
        new(
            Options.Create(new SecretProtectionOptions()),
            new DevelopmentHostEnvironment(),
            new ConfigurationBuilder().Build());

    /// <summary>SMTP テスト用の Development ホスト。</summary>
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
