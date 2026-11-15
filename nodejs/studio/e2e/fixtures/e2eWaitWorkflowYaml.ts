/** Wait 状態で止まる最小 states YAML（E2E 用）。WaitNode が ResumeEvt を 1 つ待つ。 */
export function e2eWaitWorkflowYaml(workflowName: string): string {
  return `
workflow:
  name: ${workflowName}
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
`.trim();
}

/**
 * 並列分岐の 2 つの Wait で止まる states YAML（E2E 用）。
 * @remarks Start から WaitA（EvtA を待つ）と WaitB（EvtB を待つ）へ fork し、Join1 で両方を待ってから End へ進む。
 * 子の実行は作らず、親の実行のグラフに 2 つの待機が並ぶ。
 * @param workflowName ワークフロー名。
 * @returns states YAML。
 */
export function e2eParallelWaitWorkflowYaml(workflowName: string): string {
  return `
workflow:
  name: ${workflowName}
states:
  Start:
    on:
      Completed:
        fork: [WaitA, WaitB]
  WaitA:
    wait:
      event: EvtA
    on:
      Completed:
        next: Join1
  WaitB:
    wait:
      event: EvtB
    on:
      Completed:
        next: Join1
  Join1:
    join:
      all: [WaitA, WaitB]
    on:
      Joined:
        next: End
  End:
    on:
      Completed:
        end: true
`.trim();
}
