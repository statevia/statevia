namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <c>execution_wait_subscriptions</c> の購読行（1 Wait ノードあたり 0 件以上）。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Topic"/> と <see cref="CorrelationKey"/> は正規化済み文字列。
/// 未指定の key は空文字（NULL は使わない）。照合は両列の厳密一致。
/// </para>
/// </remarks>
public sealed class ExecutionWaitSubscriptionRow
{
    /// <summary>購読行 ID（PK。列名 <c>subscription_id</c>）。</summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>実行インスタンス ID。</summary>
    public Guid ExecutionId { get; set; }

    /// <summary>待機中 Wait ノード ID。</summary>
    public required string NodeId { get; set; }

    /// <summary>購読トピック。</summary>
    public required string Topic { get; set; }

    /// <summary>相関キー（未指定は空文字）。</summary>
    public required string CorrelationKey { get; set; }

    /// <summary>Resume に渡す内部イベント名。</summary>
    public required string ResumeEventName { get; set; }

    /// <summary>行作成時刻（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 集合配送の照合で topic / key が一致した購読。
/// </summary>
/// <remarks>
/// 範囲の外の購読も返すのは、除いた件数を運用者向けのログに残すためである。
/// 範囲の外の購読の値（実行 ID・ノード ID）は Application の外へ出さない。
/// </remarks>
/// <param name="ExecutionId">対象実行 ID。</param>
/// <param name="NodeId">Wait ノード ID。</param>
/// <param name="ResumeEventName">Resume に渡す内部イベント名。</param>
/// <param name="InScope">
/// 判定する Principal が、購読の実行の定義の Project に実行者以上で所属し、
/// かつその定義が操作できる定義に入るとき <see langword="true"/>。Resume を積むのは true の購読だけ。
/// </param>
public sealed record MatchingWaitSubscription(
    Guid ExecutionId,
    string NodeId,
    string ResumeEventName,
    bool InScope);
