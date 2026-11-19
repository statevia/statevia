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
    /// <remarks>Action Host を使わずに <c>COMPLETED</c> へ収束する。直線完了と、Start 直後の Cancel の Arrange に使う。</remarks>
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
}
