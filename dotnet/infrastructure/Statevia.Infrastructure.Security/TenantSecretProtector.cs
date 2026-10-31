using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Statevia.Infrastructure.Security;

/// <summary>テナントと用途に紐づけて秘密を可逆保護する。</summary>
/// <remarks>
/// <para>永続化しない。プロセス内の呼び出し元だけが使う。HTTP コントローラは持たない。</para>
/// <para>鍵は呼び出し時にだけ検査する。起動時の <c>ValidateOnStart</c> には接続しない。</para>
/// </remarks>
internal interface ITenantSecretProtector
{
    /// <summary>平文を保護文字列にする。</summary>
    /// <param name="tenantId">テナント ID。Empty は不可。</param>
    /// <param name="purpose">用途。小文字・数字とドット区切りで 1〜64 文字。</param>
    /// <param name="plaintext">平文。空白のみは不可。UTF-8 で 4096 バイト以下。</param>
    /// <returns>バージョン、鍵 ID、nonce、タグ、暗号文を連結した標準 Base64。</returns>
    /// <exception cref="ArgumentException">テナント、用途、平文が契約外。</exception>
    /// <exception cref="InvalidOperationException">制限環境の鍵契約を満たさない。</exception>
    string Protect(Guid tenantId, string purpose, string plaintext);

    /// <summary>保護文字列を平文に戻す。</summary>
    /// <param name="tenantId">保護時と同じテナント ID。</param>
    /// <param name="purpose">保護時と同じ用途。</param>
    /// <param name="protectedPayload">保護文字列。</param>
    /// <returns>平文。</returns>
    /// <exception cref="ArgumentException">テナントまたは用途が契約外。</exception>
    /// <exception cref="InvalidOperationException">鍵契約違反、または復元失敗。復元失敗の理由は区別しない。</exception>
    string Unprotect(Guid tenantId, string purpose, string protectedPayload);
}

/// <summary>テナント ID と用途を追加認証データにする AES-256-GCM の保護。</summary>
/// <remarks>
/// <para>失敗メッセージに平文、マスター鍵、暗号文、GCM の内部例外文は含めない。</para>
/// <para>追加認証データは <c>{tenantId:N}|{purpose}</c>。保護文字列はバージョン <c>0x01</c>、鍵 ID <c>0x01</c>、12 バイト nonce、16 バイトタグ、暗号文。</para>
/// </remarks>
/// <param name="options"><c>Statevia:Secrets:EncryptionKey</c>。</param>
/// <param name="environment">Development の未設定だけ開発用鍵を使う判定。</param>
/// <param name="configuration">非空白の <c>STATEVIA_SECRETS_KEY</c> をオプションより優先する。</param>
internal sealed class TenantSecretProtector(
    IOptions<SecretProtectionOptions> options,
    IHostEnvironment environment,
    IConfiguration configuration) : ITenantSecretProtector
{
    /// <summary>平文の UTF-8 上限。SMTP パスワード等の秘密を無制限に暗号化しない。</summary>
    internal const int MaxPlaintextBytes = 4096;

    /// <summary>用途の最大文字数。</summary>
    internal const int MaxPurposeLength = 64;

    /// <summary>復元失敗の固定文。改ざん、テナント違い、用途違い、鍵違い、形式違いはこの文だけにする。</summary>
    internal const string UnwrapFailedMessage = "Tenant secret could not be restored.";

    private const byte PayloadVersion = 0x01;
    private const byte KeyId = 0x01;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int HeaderSizeBytes = 2 + NonceSizeBytes + TagSizeBytes;

    private const string EmptyTenantMessage = "Tenant id must not be empty.";
    private const string InvalidPurposeMessage = "Purpose is not an allowed identifier.";
    private const string InvalidPlaintextMessage = "Plaintext is empty or exceeds 4096 UTF-8 bytes.";

    private static readonly Regex PurposePattern = new(
        "^[a-z0-9]+(\\.[a-z0-9]+)*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    /// <inheritdoc />
    public string Protect(Guid tenantId, string purpose, string plaintext)
    {
        EnsureDependencies();
        EnsureTenantAndPurpose(tenantId, purpose);
        var plaintextBytes = EncodePlaintext(plaintext);
        var key = ResolveKey();
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSizeBytes];
            var associatedData = CreateAssociatedData(tenantId, purpose);
            using (var aes = new AesGcm(key, TagSizeBytes))
            {
                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, associatedData);
            }

            var payload = new byte[HeaderSizeBytes + ciphertext.Length];
            payload[0] = PayloadVersion;
            payload[1] = KeyId;
            nonce.CopyTo(payload.AsSpan(2));
            tag.CopyTo(payload.AsSpan(2 + NonceSizeBytes));
            ciphertext.CopyTo(payload.AsSpan(HeaderSizeBytes));
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <inheritdoc />
    public string Unprotect(Guid tenantId, string purpose, string protectedPayload)
    {
        EnsureDependencies();
        EnsureTenantAndPurpose(tenantId, purpose);
        var key = ResolveKey();
        byte[]? plaintextBytes = null;
        try
        {
            if (!TryReadPayload(protectedPayload, out var nonce, out var tag, out var ciphertext))
                throw new InvalidOperationException(UnwrapFailedMessage);

            plaintextBytes = new byte[ciphertext.Length];
            var associatedData = CreateAssociatedData(tenantId, purpose);
            try
            {
                using var aes = new AesGcm(key, TagSizeBytes);
                aes.Decrypt(nonce, ciphertext, tag, plaintextBytes, associatedData);
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException(UnwrapFailedMessage);
            }

            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            if (plaintextBytes is not null)
                CryptographicOperations.ZeroMemory(plaintextBytes);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>DI から渡された依存が null でないことを確認する。</summary>
    private void EnsureDependencies()
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
    }

    /// <summary>テナントと用途が契約内でなければ保護も復元もしない。</summary>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="purpose">用途。</param>
    private static void EnsureTenantAndPurpose(Guid tenantId, string purpose)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException(EmptyTenantMessage, nameof(tenantId));

        if (!IsAllowedPurpose(purpose))
            throw new ArgumentException(InvalidPurposeMessage, nameof(purpose));
    }

    /// <summary>平文を UTF-8 にする。空、空白のみ、上限超過は暗号化する前に拒否する。</summary>
    /// <param name="plaintext">平文。</param>
    /// <returns>UTF-8 バイト列。</returns>
    private static byte[] EncodePlaintext(string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext) || Encoding.UTF8.GetByteCount(plaintext) > MaxPlaintextBytes)
            throw new ArgumentException(InvalidPlaintextMessage, nameof(plaintext));

        return Encoding.UTF8.GetBytes(plaintext);
    }

    /// <summary>用途が許可形式か。区切りに <c>|</c> は使わない。</summary>
    /// <param name="purpose">用途。</param>
    /// <returns>許可形式なら <see langword="true"/>。</returns>
    private static bool IsAllowedPurpose(string? purpose)
    {
        if (string.IsNullOrEmpty(purpose) || purpose.Length > MaxPurposeLength)
            return false;

        try
        {
            return PurposePattern.IsMatch(purpose);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>呼び出し時に鍵を解決する。契約外なら値を含まない固定文で失敗する。</summary>
    /// <returns>32 バイト鍵。</returns>
    private byte[] ResolveKey()
    {
        var configuredKey = SecretProtectionOptions.SelectConfiguredKey(
            configuration[SecretProtectionOptions.EnvironmentVariableName],
            options.Value.EncryptionKey);
        if (!SecretProtectionOptions.TryGetAcceptedKey(configuredKey, environment, out var key))
            throw new InvalidOperationException(SecretProtectionOptions.RestrictedEnvironmentKeyMessage);

        return key;
    }

    /// <summary>追加認証データ。<c>{tenantId:N}|{purpose}</c> の UTF-8。</summary>
    /// <param name="tenantId">ハイフンなし小文字 32 文字にするテナント ID。</param>
    /// <param name="purpose">用途。</param>
    /// <returns>GCM の追加認証データ。</returns>
    private static byte[] CreateAssociatedData(Guid tenantId, string purpose) =>
        Encoding.UTF8.GetBytes(string.Concat(tenantId.ToString("N"), "|", purpose));

    /// <summary>保護文字列をバージョン、鍵 ID、nonce、タグ、暗号文に分ける。形式違いは失敗として <see langword="false"/>。</summary>
    /// <param name="protectedPayload">保護文字列。</param>
    /// <param name="nonce">12 バイト nonce。</param>
    /// <param name="tag">16 バイトタグ。</param>
    /// <param name="ciphertext">暗号文。</param>
    /// <returns>既知のバージョンと鍵 ID で、ヘッダ長を満たすとき <see langword="true"/>。</returns>
    private static bool TryReadPayload(
        string? protectedPayload,
        out byte[] nonce,
        out byte[] tag,
        out byte[] ciphertext)
    {
        nonce = [];
        tag = [];
        ciphertext = [];
        if (string.IsNullOrWhiteSpace(protectedPayload))
            return false;

        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(protectedPayload);
        }
        catch (FormatException)
        {
            return false;
        }

        if (raw.Length < HeaderSizeBytes || raw[0] != PayloadVersion || raw[1] != KeyId)
            return false;

        nonce = raw.AsSpan(2, NonceSizeBytes).ToArray();
        tag = raw.AsSpan(2 + NonceSizeBytes, TagSizeBytes).ToArray();
        ciphertext = raw.AsSpan(HeaderSizeBytes).ToArray();
        return true;
    }
}
