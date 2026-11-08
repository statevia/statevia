using Statevia.Core.Actions.Abstractions.Catalog;

namespace Statevia.Core.Actions.Abstractions.Execution;

/// <summary>Descriptor と実行コンテキストから最終実行モードを決定する。</summary>
public interface IActionExecutionPolicy
{
    /// <summary>実行モードを解決する。</summary>
    /// <param name="context">実行コンテキスト。</param>
    /// <param name="descriptor">対象 Descriptor。</param>
    /// <returns>この実行で使う実行モード。</returns>
    ActionExecutionMode Resolve(ActionExecutionContext context, ActionDescriptor descriptor);
}
