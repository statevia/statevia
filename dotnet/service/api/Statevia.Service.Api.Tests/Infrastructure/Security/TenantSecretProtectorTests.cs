using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Statevia.Infrastructure.Security.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Statevia.Service.Api.Tests.Infrastructure.Security;

/// <summary>テナント秘密の保護 API とマスター鍵契約。</summary>
public sealed class TenantSecretProtectorTests
{
    private const string Purpose = "notify.smtp";
    private const string Plaintext = "plain-secret-value-9f3a";

    /// <summary>未設定の鍵は制限環境の契約を満たさない。</summary>
    [Fact]
    public void MeetsRestrictedEnvironmentKeyPolicy_WhenUnset_ReturnsFalse()
    {
        // Act
        var accepted = SecretProtectionOptions.MeetsRestrictedEnvironmentKeyPolicy(null);

        // Assert
        Assert.False(accepted);
    }

    /// <summary>開発用鍵の Base64 は制限環境の契約を満たさない。</summary>
    [Fact]
    public void MeetsRestrictedEnvironmentKeyPolicy_WhenDevelopmentKey_ReturnsFalse()
    {
        // Act
        var accepted = SecretProtectionOptions.MeetsRestrictedEnvironmentKeyPolicy(
            SecretProtectionOptions.DevelopmentKeyBase64);

        // Assert
        Assert.False(accepted);
    }

    /// <summary>デコード結果が 32 バイトでない鍵は制限環境の契約を満たさない。</summary>
    [Fact]
    public void MeetsRestrictedEnvironmentKeyPolicy_WhenDecodedLengthIsNot32_ReturnsFalse()
    {
        // Arrange
        var shortKey = Convert.ToBase64String(new byte[16]);

        // Act
        var accepted = SecretProtectionOptions.MeetsRestrictedEnvironmentKeyPolicy(shortKey);

        // Assert
        Assert.False(accepted);
    }

    /// <summary>32 バイトの Base64 鍵は制限環境の契約を満たす。</summary>
    [Fact]
    public void MeetsRestrictedEnvironmentKeyPolicy_When32ByteKey_ReturnsTrue()
    {
        // Act
        var accepted = SecretProtectionOptions.MeetsRestrictedEnvironmentKeyPolicy(CreateKeyBase64());

        // Assert
        Assert.True(accepted);
    }

    /// <summary>Production は制限環境である。</summary>
    [Fact]
    public void IsRestrictedHostEnvironment_WhenProduction_ReturnsTrue()
    {
        // Act
        var restricted = SecretProtectionOptions.IsRestrictedHostEnvironment(
            new TestHostEnvironment(Environments.Production));

        // Assert
        Assert.True(restricted);
    }

    /// <summary>Staging は制限環境である。</summary>
    [Fact]
    public void IsRestrictedHostEnvironment_WhenStaging_ReturnsTrue()
    {
        // Act
        var restricted = SecretProtectionOptions.IsRestrictedHostEnvironment(
            new TestHostEnvironment(Environments.Staging));

        // Assert
        Assert.True(restricted);
    }

    /// <summary>Development は制限環境ではない。</summary>
    [Fact]
    public void IsRestrictedHostEnvironment_WhenDevelopment_ReturnsFalse()
    {
        // Act
        var restricted = SecretProtectionOptions.IsRestrictedHostEnvironment(
            new TestHostEnvironment(Environments.Development));

        // Assert
        Assert.False(restricted);
    }

    /// <summary>非空白の環境変数は appsettings の鍵より優先される。</summary>
    [Fact]
    public void SelectConfiguredKey_WhenEnvironmentSet_PrefersEnvironment()
    {
        // Arrange
        var boundKey = CreateKeyBase64();
        var environmentKey = CreateKeyBase64();

        // Act
        var selected = SecretProtectionOptions.SelectConfiguredKey(environmentKey, boundKey);

        // Assert
        Assert.Equal(environmentKey, selected);
    }

    /// <summary>Development で鍵未設定のとき、開発用 ASCII の 32 バイトを鍵にする。</summary>
    [Fact]
    public void TryGetAcceptedKey_WhenDevelopmentAndUnset_ReturnsDevelopmentAscii()
    {
        // Act
        var accepted = SecretProtectionOptions.TryGetAcceptedKey(
            configuredKey: null,
            new TestHostEnvironment(Environments.Development),
            out var key);

        // Assert
        Assert.True(accepted);
        Assert.Equal(Encoding.ASCII.GetBytes(SecretProtectionOptions.DevelopmentKeyAscii), key);
        Assert.Equal(SecretProtectionOptions.KeySizeBytes, SecretProtectionOptions.DevelopmentKeyAscii.Length);
    }

    /// <summary>同一テナント・同一用途で保護した文字列は平文に戻る。</summary>
    [Fact]
    public void ProtectAndUnprotect_SameTenantAndPurpose_ReturnsPlaintext()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);
        var tenantId = Guid.NewGuid();

        // Act
        var protectedPayload = protector.Protect(tenantId, Purpose, Plaintext);
        var restored = protector.Unprotect(tenantId, Purpose, protectedPayload);

        // Assert
        Assert.Equal(Plaintext, restored);
    }

    /// <summary>連続する保護文字列は nonce が変わるため一致しない。</summary>
    [Fact]
    public void Protect_CalledTwice_ReturnsDifferentPayloads()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);
        var tenantId = Guid.NewGuid();

        // Act
        var first = protector.Protect(tenantId, Purpose, Plaintext);
        var second = protector.Protect(tenantId, Purpose, Plaintext);

        // Assert
        Assert.NotEqual(first, second);
    }

    /// <summary>保護文字列はバージョン、鍵 ID、nonce、タグを先頭に置く。</summary>
    [Fact]
    public void Protect_WritesVersionKeyIdAndHeader()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);

        // Act
        var raw = Convert.FromBase64String(protector.Protect(Guid.NewGuid(), Purpose, Plaintext));

        // Assert
        Assert.Equal(0x01, raw[0]);
        Assert.Equal(0x01, raw[1]);
        Assert.True(raw.Length > 30);
    }

    /// <summary>改ざんした保護文字列は、理由を分けない復元失敗になる。</summary>
    [Fact]
    public void Unprotect_WhenPayloadTampered_ThrowsRestoreFailure()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);
        var tenantId = Guid.NewGuid();
        var raw = Convert.FromBase64String(protector.Protect(tenantId, Purpose, Plaintext));
        raw[^1] ^= 0x01;
        var tampered = Convert.ToBase64String(raw);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => protector.Unprotect(tenantId, Purpose, tampered));

        // Assert
        Assert.Equal(TenantSecretProtector.UnwrapFailedMessage, exception.Message);
        Assert.DoesNotContain(Plaintext, exception.ToString(), StringComparison.Ordinal);
    }

    /// <summary>別テナントでは復元できない。</summary>
    [Fact]
    public void Unprotect_WhenTenantDiffers_ThrowsRestoreFailure()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);
        var protectedPayload = protector.Protect(Guid.NewGuid(), Purpose, Plaintext);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => protector.Unprotect(Guid.NewGuid(), Purpose, protectedPayload));

        // Assert
        Assert.Equal(TenantSecretProtector.UnwrapFailedMessage, exception.Message);
    }

    /// <summary>別用途では復元できない。</summary>
    [Fact]
    public void Unprotect_WhenPurposeDiffers_ThrowsRestoreFailure()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);
        var tenantId = Guid.NewGuid();
        var protectedPayload = protector.Protect(tenantId, Purpose, Plaintext);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => protector.Unprotect(tenantId, "notify.other", protectedPayload));

        // Assert
        Assert.Equal(TenantSecretProtector.UnwrapFailedMessage, exception.Message);
    }

    /// <summary>別鍵では復元できない。</summary>
    [Fact]
    public void Unprotect_WhenKeyDiffers_ThrowsRestoreFailure()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var writer = CreateProtector(Environments.Production, boundKey: CreateKeyBase64());
        var reader = CreateProtector(Environments.Production, boundKey: CreateKeyBase64());
        var protectedPayload = writer.Protect(tenantId, Purpose, Plaintext);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => reader.Unprotect(tenantId, Purpose, protectedPayload));

        // Assert
        Assert.Equal(TenantSecretProtector.UnwrapFailedMessage, exception.Message);
        Assert.DoesNotContain(Plaintext, exception.ToString(), StringComparison.Ordinal);
    }

    /// <summary>空の平文は保護しない。</summary>
    [Fact]
    public void Protect_WhenPlaintextEmpty_Throws()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => protector.Protect(Guid.NewGuid(), Purpose, string.Empty));

        // Assert
        Assert.Equal("plaintext", exception.ParamName);
    }

    /// <summary>空白だけの平文は保護しない。</summary>
    [Fact]
    public void Protect_WhenPlaintextWhitespace_Throws()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => protector.Protect(Guid.NewGuid(), Purpose, "   "));

        // Assert
        Assert.Equal("plaintext", exception.ParamName);
    }

    /// <summary>UTF-8 で 4096 バイトを超える平文は保護しない。</summary>
    [Fact]
    public void Protect_WhenPlaintextExceeds4096Bytes_Throws()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);
        var plaintext = new string('a', TenantSecretProtector.MaxPlaintextBytes + 1);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => protector.Protect(Guid.NewGuid(), Purpose, plaintext));

        // Assert
        Assert.Equal("plaintext", exception.ParamName);
        Assert.DoesNotContain(plaintext, exception.ToString(), StringComparison.Ordinal);
    }

    /// <summary>UTF-8 で 4096 バイトの平文は往復できる。</summary>
    [Fact]
    public void Protect_WhenPlaintextIs4096Bytes_RoundTrips()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);
        var tenantId = Guid.NewGuid();
        var plaintext = new string('a', TenantSecretProtector.MaxPlaintextBytes);

        // Act
        var restored = protector.Unprotect(tenantId, Purpose, protector.Protect(tenantId, Purpose, plaintext));

        // Assert
        Assert.Equal(plaintext, restored);
    }

    /// <summary>空のテナントでは保護しない。</summary>
    [Fact]
    public void Protect_WhenTenantEmpty_Throws()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => protector.Protect(Guid.Empty, Purpose, Plaintext));

        // Assert
        Assert.Equal("tenantId", exception.ParamName);
    }

    /// <summary>空のテナントでは復元しない。</summary>
    [Fact]
    public void Unprotect_WhenTenantEmpty_Throws()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => protector.Unprotect(Guid.Empty, Purpose, "AAAA"));

        // Assert
        Assert.Equal("tenantId", exception.ParamName);
    }

    /// <summary>空の用途では保護しない。</summary>
    [Fact]
    public void Protect_WhenPurposeEmpty_Throws()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => protector.Protect(Guid.NewGuid(), string.Empty, Plaintext));

        // Assert
        Assert.Equal("purpose", exception.ParamName);
        Assert.DoesNotContain(Plaintext, exception.ToString(), StringComparison.Ordinal);
    }

    /// <summary>許可形式外の用途では保護しない。</summary>
    [Fact]
    public void Protect_WhenPurposeHasUppercase_Throws()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => protector.Protect(Guid.NewGuid(), "Notify.Smtp", Plaintext));

        // Assert
        Assert.Equal("purpose", exception.ParamName);
    }

    /// <summary>64 文字を超える用途では保護しない。</summary>
    [Fact]
    public void Protect_WhenPurposeExceeds64Characters_Throws()
    {
        // Arrange
        var protector = CreateProtector(Environments.Development);
        var purpose = new string('a', TenantSecretProtector.MaxPurposeLength + 1);

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => protector.Protect(Guid.NewGuid(), purpose, Plaintext));

        // Assert
        Assert.Equal("purpose", exception.ParamName);
    }

    /// <summary>Development で鍵未設定のまま保護 API を呼ぶと、開発用鍵で往復できる。</summary>
    [Fact]
    public void Protect_WhenDevelopmentAndKeyUnset_RoundTripsWithDevelopmentKey()
    {
        // Arrange
        var writer = CreateProtector(Environments.Development);
        var reader = CreateProtector(Environments.Development);
        var tenantId = Guid.NewGuid();

        // Act
        var restored = reader.Unprotect(tenantId, Purpose, writer.Protect(tenantId, Purpose, Plaintext));

        // Assert
        Assert.Equal(Plaintext, restored);
    }

    /// <summary>Development で開発用鍵を設定値として渡すと、呼び出しが失敗する。</summary>
    [Fact]
    public void Protect_WhenDevelopmentAndDevelopmentKeyConfigured_ThrowsWithoutKeyMaterial()
    {
        // Arrange
        var protector = CreateProtector(
            Environments.Development,
            boundKey: SecretProtectionOptions.DevelopmentKeyBase64);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => protector.Protect(Guid.NewGuid(), Purpose, Plaintext));

        // Assert
        AssertKeyRejectionDoesNotLeak(exception, SecretProtectionOptions.DevelopmentKeyBase64);
    }

    /// <summary>Production で鍵未設定のまま保護 API を呼ぶと失敗し、開発用鍵はメッセージに出ない。</summary>
    [Fact]
    public void Protect_WhenProductionAndKeyUnset_ThrowsWithoutKeyMaterial()
    {
        // Arrange
        var protector = CreateProtector(Environments.Production);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => protector.Protect(Guid.NewGuid(), Purpose, Plaintext));

        // Assert
        AssertKeyRejectionDoesNotLeak(exception, configuredKey: null);
        Assert.DoesNotContain(Plaintext, exception.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Production で開発用鍵を渡すと失敗し、鍵の値はメッセージに出ない。</summary>
    [Fact]
    public void Protect_WhenProductionAndDevelopmentKey_ThrowsWithoutKeyMaterial()
    {
        // Arrange
        var protector = CreateProtector(
            Environments.Production,
            boundKey: SecretProtectionOptions.DevelopmentKeyBase64);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => protector.Protect(Guid.NewGuid(), Purpose, Plaintext));

        // Assert
        AssertKeyRejectionDoesNotLeak(exception, SecretProtectionOptions.DevelopmentKeyBase64);
    }

    /// <summary>Production で 32 バイトにならない鍵を渡すと失敗し、入力した鍵はメッセージに出ない。</summary>
    [Fact]
    public void Protect_WhenProductionAndDecodedLengthIsNot32_ThrowsWithoutKeyMaterial()
    {
        // Arrange
        var configuredKey = Convert.ToBase64String(new byte[16]);
        var protector = CreateProtector(Environments.Production, boundKey: configuredKey);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => protector.Protect(Guid.NewGuid(), Purpose, Plaintext));

        // Assert
        AssertKeyRejectionDoesNotLeak(exception, configuredKey);
    }

    /// <summary>Staging で鍵未設定のまま保護 API を呼ぶと失敗する。</summary>
    [Fact]
    public void Protect_WhenStagingAndKeyUnset_ThrowsWithoutKeyMaterial()
    {
        // Arrange
        var protector = CreateProtector(Environments.Staging);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => protector.Protect(Guid.NewGuid(), Purpose, Plaintext));

        // Assert
        AssertKeyRejectionDoesNotLeak(exception, configuredKey: null);
    }

    /// <summary><c>STATEVIA_SECRETS_KEY</c> は appsettings の鍵より優先される。</summary>
    [Fact]
    public void Unprotect_WhenEnvironmentKeySet_UsesEnvironmentInsteadOfAppsettings()
    {
        // Arrange
        var appsettingsKey = CreateKeyBase64();
        var environmentKey = CreateKeyBase64();
        var tenantId = Guid.NewGuid();
        var writer = CreateProtector(
            Environments.Production,
            boundKey: appsettingsKey,
            environmentKey: environmentKey);
        var environmentReader = CreateProtector(Environments.Production, boundKey: environmentKey);
        var appsettingsReader = CreateProtector(Environments.Production, boundKey: appsettingsKey);
        var protectedPayload = writer.Protect(tenantId, Purpose, Plaintext);

        // Act
        var restored = environmentReader.Unprotect(tenantId, Purpose, protectedPayload);
        var appsettingsFailure = Assert.Throws<InvalidOperationException>(
            () => appsettingsReader.Unprotect(tenantId, Purpose, protectedPayload));

        // Assert
        Assert.Equal(Plaintext, restored);
        Assert.Equal(TenantSecretProtector.UnwrapFailedMessage, appsettingsFailure.Message);
    }

    /// <summary>鍵未設定でも起動検証は成功し、Development では保護 API を解決して往復できる。</summary>
    [Fact]
    public void AddStateviaInfrastructureSecurity_WhenKeyUnset_ResolvesProtectorWithoutStartupFailure()
    {
        // Arrange
        using var provider = CreateSecurityProvider(Environments.Development);

        // Act
        var startupFailure = Record.Exception(() =>
        {
            foreach (var validator in provider.GetServices<IStartupValidator>())
                validator.Validate();
        });
        var protector = provider.GetRequiredService<ITenantSecretProtector>();
        var tenantId = Guid.NewGuid();
        var restored = protector.Unprotect(tenantId, Purpose, protector.Protect(tenantId, Purpose, Plaintext));

        // Assert
        Assert.Null(startupFailure);
        Assert.Equal(Plaintext, restored);
    }

    /// <summary>Production で鍵未設定でも起動検証は成功し、保護 API の呼び出し時だけ失敗する。</summary>
    [Fact]
    public void AddStateviaInfrastructureSecurity_WhenProductionAndKeyUnset_ValidatesOnCallOnly()
    {
        // Arrange
        using var provider = CreateSecurityProvider(Environments.Production);

        // Act
        var startupFailure = Record.Exception(() =>
        {
            foreach (var validator in provider.GetServices<IStartupValidator>())
                validator.Validate();
        });
        var protector = provider.GetRequiredService<ITenantSecretProtector>();
        var callFailure = Assert.Throws<InvalidOperationException>(
            () => protector.Protect(Guid.NewGuid(), Purpose, Plaintext));

        // Assert
        Assert.Null(startupFailure);
        Assert.Equal(SecretProtectionOptions.RestrictedEnvironmentKeyMessage, callFailure.Message);
    }

    /// <summary>32 バイト鍵の標準 Base64 を作る。</summary>
    /// <returns>保護 API が受理する設定文字列。</returns>
    private static string CreateKeyBase64()
    {
        var bytes = new byte[SecretProtectionOptions.KeySizeBytes];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>鍵の拒否例外に、開発用鍵と入力した鍵が含まれないことを確認する。</summary>
    /// <param name="exception">保護 API が投げた例外。</param>
    /// <param name="configuredKey">呼び出しに渡した設定値。未設定なら null。</param>
    private static void AssertKeyRejectionDoesNotLeak(InvalidOperationException exception, string? configuredKey)
    {
        Assert.Equal(SecretProtectionOptions.RestrictedEnvironmentKeyMessage, exception.Message);
        var text = exception.ToString();
        Assert.DoesNotContain(SecretProtectionOptions.DevelopmentKeyAscii, text, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretProtectionOptions.DevelopmentKeyBase64, text, StringComparison.Ordinal);
        if (configuredKey is not null)
            Assert.DoesNotContain(configuredKey, text, StringComparison.Ordinal);
    }

    /// <summary>オプションと環境を直接渡した保護 API を作る。</summary>
    /// <param name="environmentName">ホスト環境名。</param>
    /// <param name="boundKey"><c>Statevia:Secrets:EncryptionKey</c>。未設定は null。</param>
    /// <param name="environmentKey"><c>STATEVIA_SECRETS_KEY</c>。未設定は null。</param>
    /// <returns>保護 API。</returns>
    private static TenantSecretProtector CreateProtector(
        string environmentName,
        string? boundKey = null,
        string? environmentKey = null)
    {
        var values = new Dictionary<string, string?>();
        if (environmentKey is not null)
            values[SecretProtectionOptions.EnvironmentVariableName] = environmentKey;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new TenantSecretProtector(
            Options.Create(new SecretProtectionOptions { EncryptionKey = boundKey }),
            new TestHostEnvironment(environmentName),
            configuration);
    }

    /// <summary>保護 API の DI 登録だけを持つプロバイダを作る。起動検証は付けない。</summary>
    /// <param name="environmentName">ホスト環境名。</param>
    /// <returns>サービスプロバイダ。</returns>
    private static ServiceProvider CreateSecurityProvider(string environmentName)
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddStateviaInfrastructureSecurity(configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>テスト用のホスト環境。</summary>
    /// <param name="environmentName">環境名。</param>
    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        /// <inheritdoc />
        public string EnvironmentName { get; set; } = environmentName;

        /// <inheritdoc />
        public string ApplicationName { get; set; } = "Statevia.Service.Api.Tests";

        /// <inheritdoc />
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        /// <inheritdoc />
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
