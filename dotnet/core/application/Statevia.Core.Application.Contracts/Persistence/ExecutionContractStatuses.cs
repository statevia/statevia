namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// HTTP の応答と <c>status</c> フィルタで使う実行ステータスの値（契約の値）。
/// </summary>
/// <remarks>
/// <para>
/// 実行ステータスは、投影（<c>executions.status</c>。<see cref="ExecutionProjectionStatuses"/>）と HTTP の二層で綴りが違う。
/// HTTP には投影の値（<c>Running</c> / <c>Cancelled</c> など）を出さず、本クラスの値だけを出す。
/// 変換は <see cref="ExecutionStatusMapper"/> だけが行い、Engine と永続化は投影の値のまま使う。
/// </para>
/// <para>
/// 綴りはノードの状態にそろえて <c>CANCELED</c> とする。<see cref="Unknown"/> は応答だけに出る値で、
/// <c>status</c> フィルタの許可値には含めない（投影に <c>Unknown</c> は書かれず、絞り込んでも必ず 0 件になるため）。
/// </para>
/// </remarks>
public static class ExecutionContractStatuses
{
    /// <summary>終端していない（投影の <c>Running</c>）。</summary>
    public const string Active = "ACTIVE";

    /// <summary>正常に終わった（投影の <c>Completed</c>）。</summary>
    public const string Completed = "COMPLETED";

    /// <summary>失敗で終わった（投影の <c>Failed</c>）。</summary>
    public const string Failed = "FAILED";

    /// <summary>キャンセルで終わった（投影の <c>Cancelled</c>）。</summary>
    public const string Canceled = "CANCELED";

    /// <summary>投影の値が想定外で、判定できない。応答だけに出し、フィルタでは受けない。</summary>
    public const string Unknown = "UNKNOWN";
}
