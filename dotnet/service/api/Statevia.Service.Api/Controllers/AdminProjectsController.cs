using Microsoft.AspNetCore.Mvc;

namespace Statevia.Service.Api.Controllers;

/// <summary>
/// テナント管理者向け Project 一覧（<c>GET /v1/admin/projects</c>）。
/// </summary>
/// <remarks>未所属の Project を含む。定義・実行の第二ゲートは開かない。</remarks>
/// <param name="projects">Project ユースケース。</param>
[ApiController]
[Route("v1/admin/projects")]
public sealed class AdminProjectsController(IProjectService projects) : ControllerBase
{
    /// <summary>GET /v1/admin/projects — テナント内の全 Project。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> List(CancellationToken ct) =>
        Ok(await projects.ListAllForTenantAdminAsync(ct).ConfigureAwait(false));
}
