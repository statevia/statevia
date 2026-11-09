using Statevia.Service.Api.Contracts.Admin;

namespace Statevia.Service.Api.Abstractions.Services;

/// <summary>Module load 状態の参照と明示 reload。</summary>
public interface IModuleManagementService
{
    /// <summary>ModuleHost へ discover / load を再実行する。</summary>
    /// <param name="cancellationToken">キャンセル。</param>
    /// <returns>discover と load が終わると完了するタスク。</returns>
    Task ReloadAsync(CancellationToken cancellationToken);

    /// <summary>load catalog のスナップショットを返す。</summary>
    /// <returns>Module の一覧。</returns>
    IReadOnlyList<AdminModuleListItemDto> ListModules();
}
