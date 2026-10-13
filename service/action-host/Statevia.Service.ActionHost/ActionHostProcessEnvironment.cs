using System.Collections;

namespace Statevia.Service.ActionHost;

/// <summary>
/// Action Host の待受前に、プロセス環境を拒否または削除する。
/// </summary>
/// <remarks>
/// <para>Module ロードと設定バインドより前に一度だけ呼ぶ。ユーザー環境とマシン環境は変更しない。</para>
/// <para>禁止名に空白以外の値があるときは起動失敗にする。それ以外の許可リスト外は削除して起動を続ける。</para>
/// <para>名前の比較は大文字小文字を区別しない。Windows のプロセス環境は同一名を区別しないため。</para>
/// </remarks>
internal static class ActionHostProcessEnvironment
{
    private const string RefusalPrefix =
        "Action Host refused to start because these environment variables are set: ";

    private static readonly string[] DeniedNames =
    [
        "DATABASE_URL",
        "STATEVIA_SECRETS_KEY",
        "Statevia__Secrets__EncryptionKey",
        "STATEVIA_SMTP_HOST",
        "STATEVIA_SMTP_PORT",
        "STATEVIA_SMTP_USER",
        "STATEVIA_SMTP_PASSWORD",
        "STATEVIA_SMTP_FROM",
        "STATEVIA_NOTIFICATION_SMTP_SOURCE",
        "STATEVIA_BOOTSTRAP_PASSWORD",
        "Auth__Jwt__SigningKey",
    ];

    private static readonly string[] AllowedNames =
    [
        "STATEVIA_MODULES_PATH",
        "PATH",
        "HOME",
        "USER",
        "HOSTNAME",
        "LANG",
        "LC_ALL",
        "TZ",
        "TMP",
        "TEMP",
        "TMPDIR",
        "SSL_CERT_FILE",
        "SSL_CERT_DIR",
    ];

    private static readonly string[] AllowedPrefixes =
    [
        "ASPNETCORE_",
        "DOTNET_",
        "COMPlus_",
    ];

    /// <summary>プロセス環境を拒否または削除する。</summary>
    /// <exception cref="InvalidOperationException">禁止名に空白以外の値があるとき。メッセージに値は含めない。</exception>
    public static void ApplyToProcess()
    {
        var plan = CreatePlan(ReadProcessVariables());
        if (plan.RefusedNames.Count > 0)
        {
            throw new InvalidOperationException(RefusalPrefix + string.Join(", ", plan.RefusedNames));
        }

        foreach (var name in plan.NamesToRemove)
        {
            Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Process);
        }
    }

    /// <summary>起動時に拒否する名前と、削除する名前を決める。プロセス環境は変更しない。</summary>
    /// <param name="variables">名前と値。未設定は含めない。</param>
    /// <returns>拒否名と削除名。拒否名はソート済み。</returns>
    internal static StartupPlan CreatePlan(IEnumerable<KeyValuePair<string, string?>> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var denied = new List<string>();
        var remove = new List<string>();
        foreach (var variable in variables)
        {
            if (IsDenied(variable.Key, variable.Value))
            {
                denied.Add(variable.Key);
                continue;
            }

            if (!IsAllowed(variable.Key))
            {
                remove.Add(variable.Key);
            }
        }

        denied.Sort(StringComparer.Ordinal);
        return new StartupPlan(denied, remove);
    }

    private static List<KeyValuePair<string, string?>> ReadProcessVariables()
    {
        var variables = new List<KeyValuePair<string, string?>>();
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process))
        {
            if (entry.Key is not string name)
            {
                continue;
            }

            var value = entry.Value is string text ? text : null;
            variables.Add(new KeyValuePair<string, string?>(name, value));
        }

        return variables;
    }

    private static bool IsDenied(string name, string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DeniedNames.Any(denied => string.Equals(denied, name, StringComparison.OrdinalIgnoreCase));

    private static bool IsAllowed(string name) =>
        AllowedNames.Any(allowed => string.Equals(allowed, name, StringComparison.OrdinalIgnoreCase))
        || AllowedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>起動前の環境変数の扱い。</summary>
    /// <param name="RefusedNames">空白以外の値がある禁止名。起動失敗の理由。</param>
    /// <param name="NamesToRemove">許可リスト外で、プロセス環境から消す名前。</param>
    internal readonly record struct StartupPlan(
        IReadOnlyList<string> RefusedNames,
        IReadOnlyList<string> NamesToRemove);
}
