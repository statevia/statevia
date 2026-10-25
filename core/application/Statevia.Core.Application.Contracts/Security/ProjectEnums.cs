namespace Statevia.Core.Application.Contracts.Security;

/// <summary>project_accesses.role の値。数値順が権限強度（Reader &lt; Executor &lt; Publisher &lt; Admin）。</summary>
public enum ProjectAccessRole
{
    /// <summary>定義の読み取り（一覧・取得・グラフ参照）。</summary>
    Reader = 0,

    /// <summary>ワークフロー開始。</summary>
    Executor = 1,

    /// <summary>定義 publish（版追加）。</summary>
    Publisher = 2,

    /// <summary>プロジェクトの設定とメンバー管理。</summary>
    Admin = 3
}
