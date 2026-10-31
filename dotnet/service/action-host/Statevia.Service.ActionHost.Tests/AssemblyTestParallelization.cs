// プロセス環境変数はテスト間で共有される状態である。ActionHostExecutor は Action 実行後に
// ProcessEnvironmentBaseline.Restore() で基準に無い変数を削除するため、並列実行すると
// 他のテストが設定した STATEVIA_MODULES_PATH などが途中で消える。アセンブリ全体を直列にする。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
