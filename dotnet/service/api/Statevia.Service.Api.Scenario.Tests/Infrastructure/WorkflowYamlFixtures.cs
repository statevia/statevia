namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>
/// シナリオテスト用のワークフロー定義 YAML フィクスチャ。
/// </summary>
/// <remarks>
/// Cancel シナリオ用に <c>wait.event</c> で停止する最小ワークフローを提供する。
/// ポーリング中に Cancel することで <c>Cancelled</c> ステータスへ遷移させる。
/// </remarks>
public static class WorkflowYamlFixtures
{
    /// <summary>
    /// action 省略（builtin noop）の 2 状態を直線につなぎ、Wait なしで完了する最小ワークフロー定義 YAML を返す。
    /// </summary>
    /// <remarks>Action Host を使わずに <c>COMPLETED</c> へ収束する。直線完了の Arrange に使う。</remarks>
    /// <param name="nameSuffix">定義名に付与する一意サフィックス（Guid 推奨）。</param>
    /// <returns>YAML 文字列。</returns>
    public static string LinearNoopWorkflow(string nameSuffix) => $"""
        workflow:
          name: scenario-linear-{nameSuffix}
        states:
          Start:
            on:
              Completed:
                next: Finish
          Finish:
            on:
              Completed:
                end: true
        """;

    /// <summary>
    /// <c>wait.event</c> で無期限停止する最小ワークフロー定義 YAML を返す。
    /// </summary>
    /// <param name="nameSuffix">定義名に付与する一意サフィックス（Guid 推奨）。</param>
    /// <returns>YAML 文字列。</returns>
    public static string WaitWorkflow(string nameSuffix) => $"""
        workflow:
          name: scenario-wait-{nameSuffix}
        states:
          Start:
            on:
              Completed:
                next: WaitNode
          WaitNode:
            wait:
              event: ResumeEvt
            on:
              Completed:
                next: End
          End:
            on:
              Completed:
                end: true
        """;

    /// <summary>
    /// 並列分岐の 2 つの枝が同じイベント名 <c>Go</c> で待つワークフロー定義 YAML を返す。
    /// </summary>
    /// <remarks>
    /// 親の ID へ <c>Go</c> を送ると、当てはまる子の待機が 2 件になり届け先が決まらない（422）。
    /// 届け先の解決が認可より前に走らないことの確認に使う。
    /// </remarks>
    /// <param name="nameSuffix">定義名に付与する一意サフィックス（Guid 推奨）。</param>
    /// <returns>YAML 文字列。</returns>
    public static string ForkTwoWaitsSameEventWorkflow(string nameSuffix) => $"""
        workflow:
          name: scenario-fork-waits-{nameSuffix}
        states:
          Start:
            on:
              Completed:
                fork: [AskA, AskB]
          AskA:
            wait:
              events:
                Go: Join1
          AskB:
            wait:
              events:
                Go: Join1
          Join1:
            join:
              all: [AskA, AskB]
            on:
              Joined:
                next: End
          End:
            on:
              Completed:
                end: true
        """;
}
