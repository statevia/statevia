namespace Statevia.Core.Application.Contracts.Services;

/// <summary>Action Schema HTTP API の供給サービス。</summary>
public interface IActionSchemaService
{
    /// <summary>登録 action 一覧と descriptor 概要を返す。</summary>
    /// <returns>action の一覧。</returns>
    ActionSchemaListResponse GetList();

    /// <summary>Playground 向け軽量 index を返す。</summary>
    /// <returns>action の index。</returns>
    ActionSchemaIndexResponse GetIndex();

    /// <summary>指定 actionId の schema 詳細を返す。未登録は <see cref="NotFoundException"/>。</summary>
    /// <param name="actionId">canonical actionId またはエイリアス。</param>
    /// <returns>schema の詳細。</returns>
    ActionSchemaDetailResponse GetDetail(string actionId);
}
