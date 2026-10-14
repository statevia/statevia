namespace Statevia.Service.ActionHost.Execution;

/// <summary>プロセス環境の読み書き。</summary>
/// <remarks>ユーザー環境とマシン環境は対象にしない。</remarks>
internal interface IProcessEnvironment
{
    /// <summary>現在のプロセス環境を読む。</summary>
    /// <returns>名前と値。未設定の名前は含めない。</returns>
    IReadOnlyDictionary<string, string> Read();

    /// <summary>プロセス環境を更新する。</summary>
    /// <param name="name">環境変数名。</param>
    /// <param name="value">書き込む値。null は削除。</param>
    void Set(string name, string? value);
}
