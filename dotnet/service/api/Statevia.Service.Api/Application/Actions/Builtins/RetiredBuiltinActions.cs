namespace Statevia.Service.Api.Application.Actions.Builtins;

/// <summary>
/// 退役した Builtin Action と、代わりに使う手段の説明。
/// </summary>
/// <remarks>
/// 退役した Action は Catalog に <c>IsRetired</c> 付きで残る（保存済みの版を読み込むため）。
/// 保存・検証の 422 と実行時の失敗は、ここの説明文を同じ形で添える。
/// </remarks>
internal static class RetiredBuiltinActions
{
    /// <summary>
    /// <see cref="WellKnownActionIds.Signal"/> の代わりの手段。
    /// signal は Wait を再開できなかったため退役した。
    /// </summary>
    public const string SignalReplacementHint =
        "It never resumed a wait. Resume the wait with POST /v1/executions/{id}/nodes/{nodeId}/resume, "
        + "or use wait.subscribe with statevia.action.builtin.event.publish.";

    private static readonly Dictionary<string, string> s_replacementHints = new(StringComparer.Ordinal)
    {
        [WellKnownActionIds.Signal] = SignalReplacementHint,
    };

    /// <summary>
    /// 退役した Action を新しい定義で使ったときのエラーメッセージを組み立てる。
    /// </summary>
    /// <param name="actionId">退役した Action の ID。</param>
    /// <param name="stateName">その Action を使っている状態名。</param>
    /// <returns>英語のメッセージ。代わりの手段が分かる Action はその説明を添える。</returns>
    public static string FormatRejection(string actionId, string stateName) =>
        s_replacementHints.TryGetValue(actionId, out var hint)
            ? $"Action '{actionId}' in state '{stateName}' is retired. {hint}"
            : $"Action '{actionId}' in state '{stateName}' is retired.";
}
