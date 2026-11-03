namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// Hosted 物理 Join 充足時に親 Execution Context へ投影する子 1 件分の断片。
/// </summary>
/// <remarks>
/// <para>リスト順が適用順（枝の完了順）。同一 <c>states</c> 名・同一または重なる <c>vars</c> パスは後勝ち。</para>
/// <para>その枝が書いた値だけを持つ。Fork 到達時に親から引き継いだだけの値は含めない。
/// 含めると、他の枝が書いた新しい値を古い値で上書きし得る。</para>
/// </remarks>
/// <param name="States">枝で完了した State エントリ（stateName → `{ output: … }`）。</param>
/// <param name="VarWrites">枝で書いた <c>vars</c> のパスと、子終端時点の値。</param>
public sealed record PhysicalJoinContextFragment(
    IReadOnlyDictionary<string, object?>? States,
    IReadOnlyList<ContextVarWrite>? VarWrites);

/// <summary>
/// <c>vars</c> の 1 パスへの書き込み。
/// </summary>
/// <remarks>
/// <para>代入はパスの部分木の置き換え。<c>order</c> への書き込みは、それより前に適用した <c>order.status</c> を含めて上書きする。</para>
/// </remarks>
/// <param name="Path"><c>$.vars</c> 以下のプロパティ名の並び（<c>$.vars.order.status</c> なら <c>["order", "status"]</c>）。空は <c>vars</c> 全体。</param>
/// <param name="Value">書き込む値（辞書ツリー、または同形の <see cref="System.Text.Json.JsonElement"/>）。</param>
public sealed record ContextVarWrite(IReadOnlyList<string> Path, object? Value);
