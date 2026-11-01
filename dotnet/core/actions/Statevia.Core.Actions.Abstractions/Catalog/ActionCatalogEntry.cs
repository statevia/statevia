using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>Catalog 登録時の実行エントリ（Phase 1 は InProcess のみ）。</summary>
/// <param name="Aliases">canonical 以外の解決用エイリアス。</param>
/// <param name="InProcessFactory">InProcess 実行時の <see cref="IStateExecutor"/> ファクトリ。</param>
/// <param name="IsRetired">
/// 退役した Action か。退役した Action は、新しい定義の保存・更新・検証では使えず、Action 一覧と Schema API にも出ない。
/// 保存済みの版は読み込めるようにエントリだけ残し、実行時は退役用の実行器がノードを失敗させる。
/// </param>
public sealed record ActionCatalogEntry(
    IReadOnlyList<string>? Aliases = null,
    Func<IServiceProvider, IStateExecutor>? InProcessFactory = null,
    bool IsRetired = false);
