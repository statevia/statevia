namespace Statevia.Service.Api.Bootstrap;

/// <summary>create-admin コマンドの引数。</summary>
internal sealed class CreateAdminCliOptions
{
    /// <summary>ヘルプ表示。</summary>
    public bool ShowHelp { get; init; }

    /// <summary>ヘルプのみ。</summary>
    public bool IsHelpOnly { get; init; }

    /// <summary>テナントキー。</summary>
    public string TenantKey { get; init; } = TenantRequestHeaders.DefaultTenantId;

    /// <summary>ログインユーザー名。</summary>
    public string Username { get; init; } = "";

    /// <summary>平文パスワード。</summary>
    public string? Password { get; init; }

    /// <summary>Principal 表示名。</summary>
    public string? DisplayName { get; init; }

    /// <summary>既存ならスキップ。</summary>
    public bool SkipIfExists { get; init; }

    /// <summary>引数を解析する。</summary>
    /// <param name="args">コマンド名より後ろの引数。</param>
    /// <returns>解析結果。未知の引数や必須の値が無いときは <see cref="ShowHelp"/> が <see langword="true"/>。</returns>
    /// <exception cref="ArgumentException">値を取るオプションの後に値が無いとき。</exception>
    public static CreateAdminCliOptions Parse(string[] args)
    {
        var tenantKey = TenantRequestHeaders.DefaultTenantId;
        var username = "";
        string? password = null;
        string? displayName = null;
        var skipIfExists = false;
        var showHelp = false;
        var isHelpOnly = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    isHelpOnly = true;
                    break;
                case "--tenant-key":
                    tenantKey = CliArgReader.RequireValue(args, ref index, arg);
                    break;
                case "--username":
                    username = CliArgReader.RequireValue(args, ref index, arg);
                    break;
                case "--password":
                    password = CliArgReader.RequireValue(args, ref index, arg);
                    break;
                case "--display-name":
                    displayName = CliArgReader.RequireValue(args, ref index, arg);
                    break;
                case "--skip-if-exists":
                    skipIfExists = true;
                    break;
                default:
                    showHelp = true;
                    break;
            }
        }

        if (!showHelp && string.IsNullOrWhiteSpace(username))
            showHelp = true;

        return new CreateAdminCliOptions
        {
            ShowHelp = showHelp,
            IsHelpOnly = isHelpOnly,
            TenantKey = tenantKey,
            Username = username,
            Password = password,
            DisplayName = displayName,
            SkipIfExists = skipIfExists
        };
    }
}
