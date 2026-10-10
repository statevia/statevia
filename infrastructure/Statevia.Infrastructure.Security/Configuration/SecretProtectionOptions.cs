using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;

namespace Statevia.Infrastructure.Security.Configuration;

/// <summary>テナント秘密のマスター鍵設定。</summary>
/// <remarks>
/// <para>
/// 設定キーは <c>Statevia:Secrets:EncryptionKey</c>。非空白の環境変数 <c>STATEVIA_SECRETS_KEY</c> が優先される。
/// 起動時検証には接続しない。Production / Staging では保護 API の呼び出し時だけ、未設定・開発用鍵・32 バイト以外を拒否する。
/// </para>
/// <para>Development で鍵未設定のとき、保護 API はリポジトリ既知の 32 バイト ASCII を鍵にする。その Base64 を設定値として渡すことは拒否する。</para>
/// </remarks>
internal sealed class SecretProtectionOptions
{
    /// <summary>設定セクション名。<c>EncryptionKey</c> と合わせて <c>Statevia:Secrets:EncryptionKey</c> になる。</summary>
    public const string SectionName = "Statevia:Secrets";

    /// <summary>appsettings より優先する環境変数名。</summary>
    public const string EnvironmentVariableName = "STATEVIA_SECRETS_KEY";

    /// <summary>AES-256 の鍵長。これ以外のデコード結果は拒否する。</summary>
    internal const int KeySizeBytes = 32;

    /// <summary>
    /// 設定文字列の上限。32 バイト鍵の標準 Base64 は 44 文字であり、過大な入力をデコード前に拒否する。
    /// </summary>
    internal const int MaxConfiguredKeyCharacters = 128;

    /// <summary>
    /// Development で鍵未設定のときに使う ASCII 鍵。32 バイトであり、リポジトリ既知のため Production / Staging では使えない。
    /// </summary>
    internal const string DevelopmentKeyAscii = "dev-only-statevia-secrets-key!!!";

    /// <summary>設定値として比較する開発用鍵。<see cref="DevelopmentKeyAscii"/> の標準 Base64。</summary>
    internal static readonly string DevelopmentKeyBase64 =
        Convert.ToBase64String(Encoding.ASCII.GetBytes(DevelopmentKeyAscii));

    /// <summary>鍵拒否メッセージ。鍵の値は含めない。Production / Staging の未設定・開発用鍵・32 バイト以外に使う。</summary>
    internal const string RestrictedEnvironmentKeyMessage =
        "Statevia:Secrets:EncryptionKey must be a 32-byte key other than the development default.";

    /// <summary>appsettings の鍵。未設定は null。開発用 ASCII を既定値にはしない。</summary>
    public string? EncryptionKey { get; set; }

    /// <summary>既定鍵または 32 バイトでない鍵を拒否する環境か。</summary>
    /// <param name="environment">ホスト環境。</param>
    /// <returns>Production または Staging なら <see langword="true"/>。</returns>
    internal static bool IsRestrictedHostEnvironment(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.IsProduction() || environment.IsStaging();
    }

    /// <summary>環境変数が非空白ならそれを、そうでなければ bind された鍵を返す。</summary>
    /// <param name="environmentValue"><c>STATEVIA_SECRETS_KEY</c> の値。</param>
    /// <param name="boundKey"><c>Statevia:Secrets:EncryptionKey</c> の値。</param>
    /// <returns>優先後の設定文字列。両方未設定なら bind 側の値。</returns>
    internal static string? SelectConfiguredKey(string? environmentValue, string? boundKey)
    {
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return environmentValue;

        return boundKey;
    }

    /// <summary>制限環境で受理できる鍵か。失敗理由の文面に鍵値を含めないこと。</summary>
    /// <param name="encryptionKey">優先後の設定文字列。</param>
    /// <returns>開発用 Base64 でなく、デコード結果が 32 バイトなら <see langword="true"/>。</returns>
    internal static bool MeetsRestrictedEnvironmentKeyPolicy(string? encryptionKey)
    {
        if (!TryDecodeConfiguredKey(encryptionKey, out var key))
            return false;

        CryptographicOperations.ZeroMemory(key);
        return true;
    }

    /// <summary>
    /// 呼び出しに使う 32 バイト鍵を決める。Development かつ未設定のときだけ開発用 ASCII を返す。
    /// </summary>
    /// <param name="configuredKey">環境変数優先後の設定値。</param>
    /// <param name="environment">ホスト環境。</param>
    /// <param name="key">受理した鍵。失敗時は空。</param>
    /// <returns>受理できたとき <see langword="true"/>。</returns>
    internal static bool TryGetAcceptedKey(string? configuredKey, IHostEnvironment environment, out byte[] key)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (!IsRestrictedHostEnvironment(environment) && string.IsNullOrWhiteSpace(configuredKey))
        {
            key = Encoding.ASCII.GetBytes(DevelopmentKeyAscii);
            return true;
        }

        return TryDecodeConfiguredKey(configuredKey, out key);
    }

    /// <summary>設定文字列を 32 バイト鍵として解釈する。開発用 Base64 は拒否する。</summary>
    /// <param name="encryptionKey">優先後の設定文字列。</param>
    /// <param name="key">受理した鍵。失敗時は空。</param>
    /// <returns>32 バイトにデコードできたとき <see langword="true"/>。</returns>
    private static bool TryDecodeConfiguredKey(string? encryptionKey, out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(encryptionKey))
            return false;

        if (encryptionKey.Length > MaxConfiguredKeyCharacters)
            return false;

        if (string.Equals(encryptionKey, DevelopmentKeyBase64, StringComparison.Ordinal))
            return false;

        var buffer = new byte[KeySizeBytes];
        if (!Convert.TryFromBase64String(encryptionKey, buffer, out var written) || written != KeySizeBytes)
        {
            CryptographicOperations.ZeroMemory(buffer);
            return false;
        }

        key = buffer;
        return true;
    }
}
