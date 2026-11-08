using Statevia.Core.Actions.Abstractions.Publication;
using System.Diagnostics.CodeAnalysis;

namespace Statevia.Core.Actions.Abstractions.Catalog;

/// <summary>actionId → Descriptor 解決。認可・Mode 決定は行わない。</summary>
public interface IActionCatalog
{
    /// <summary>actionId（canonical またはエイリアス）が登録済みか。</summary>
    /// <param name="actionId">参照 actionId。</param>
    /// <returns>登録済みなら <see langword="true"/>。</returns>
    bool Exists(string actionId);

    /// <summary>Descriptor を取得する。</summary>
    /// <param name="actionId">参照 actionId。</param>
    /// <param name="descriptor">取得した Descriptor。</param>
    /// <returns>見つかれば <see langword="true"/>。見つからなければ <see langword="false"/> で、<paramref name="descriptor"/> は <see langword="null"/>。</returns>
    bool TryGetDescriptor(string actionId, [NotNullWhen(true)] out ActionDescriptor? descriptor);

    /// <summary>版付きキーで Descriptor を取得する（exact lookup）。</summary>
    /// <param name="moduleId">Module ID。</param>
    /// <param name="version">fullVersion。</param>
    /// <param name="actionName">Action 名。</param>
    /// <param name="descriptor">取得した Descriptor。</param>
    /// <returns>見つかれば <see langword="true"/>。見つからなければ <see langword="false"/> で、<paramref name="descriptor"/> は <see langword="null"/>。</returns>
    bool TryGetDescriptor(
        string moduleId,
        string version,
        string actionName,
        [NotNullWhen(true)] out ActionDescriptor? descriptor);

    /// <summary>Descriptor と実行エントリを取得する。</summary>
    /// <param name="actionId">参照 actionId。</param>
    /// <param name="registration">取得した登録情報。</param>
    /// <returns>見つかれば <see langword="true"/>。見つからなければ <see langword="false"/> で、<paramref name="registration"/> は <see langword="null"/>。</returns>
    bool TryGetRegistration(string actionId, [NotNullWhen(true)] out ActionRegistration? registration);

    /// <summary>版付きキーで Descriptor と実行エントリを取得する（exact lookup）。</summary>
    /// <param name="moduleId">Module ID。</param>
    /// <param name="version">fullVersion。</param>
    /// <param name="actionName">Action 名。</param>
    /// <param name="registration">取得した登録情報。</param>
    /// <returns>見つかれば <see langword="true"/>。見つからなければ <see langword="false"/> で、<paramref name="registration"/> は <see langword="null"/>。</returns>
    bool TryGetRegistration(
        string moduleId,
        string version,
        string actionName,
        [NotNullWhen(true)] out ActionRegistration? registration);

    /// <summary>ロード済み Module の版一覧を返す（安定ソート）。</summary>
    /// <param name="moduleId">Module ID。</param>
    /// <returns>ロード済みの fullVersion を序数比較の昇順で並べた一覧。ロードされていなければ空。</returns>
    IReadOnlyList<string> GetLoadedVersions(string moduleId);

    /// <summary>Action を Catalog へ登録する。</summary>
    /// <param name="descriptor">Descriptor（不変条件は実装側で検証）。</param>
    /// <param name="entry">実行エントリ。</param>
    void Register(ActionDescriptor descriptor, ActionCatalogEntry entry);

    /// <summary>Schema Publication 付きで Action を Catalog へ登録する。</summary>
    /// <param name="descriptor">Descriptor（不変条件は実装側で検証）。</param>
    /// <param name="entry">実行エントリ。</param>
    /// <param name="publication">input/output schema と UI メタデータ。</param>
    void Register(ActionDescriptor descriptor, ActionCatalogEntry entry, ActionPublication publication);

    /// <summary>登録済み action の ActionPublication を取得する。</summary>
    /// <param name="actionId">参照 actionId（canonical またはエイリアス）。</param>
    /// <param name="publication">取得した Publication。</param>
    /// <returns>見つかれば <see langword="true"/>。見つからないか Publication が無ければ <see langword="false"/> で、<paramref name="publication"/> は <see langword="null"/>。</returns>
    bool TryGetPublication(string actionId, [NotNullWhen(true)] out ActionPublication? publication);

    /// <summary>版付きキーで ActionPublication を取得する（exact lookup）。</summary>
    /// <param name="moduleId">Module ID。</param>
    /// <param name="version">fullVersion。</param>
    /// <param name="actionName">Action 名。</param>
    /// <param name="publication">取得した Publication。</param>
    /// <returns>見つかれば <see langword="true"/>。見つからないか Publication が無ければ <see langword="false"/> で、<paramref name="publication"/> は <see langword="null"/>。</returns>
    bool TryGetPublication(
        string moduleId,
        string version,
        string actionName,
        [NotNullWhen(true)] out ActionPublication? publication);

    /// <summary>登録済み canonical actionId の一覧を安定ソートで返す。</summary>
    /// <returns>canonical actionId を序数比較の昇順で並べた一覧。エイリアスは含まない。</returns>
    IReadOnlyList<string> GetRegisteredActionIds();
}
