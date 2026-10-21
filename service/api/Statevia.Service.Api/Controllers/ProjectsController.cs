using Microsoft.AspNetCore.Mvc;

namespace Statevia.Service.Api.Controllers;

/// <summary>
/// テナント内 Project REST API（<c>/v1/projects</c>）。
/// </summary>
/// <remarks>
/// <para>一覧・取得は呼び出し元がメンバーの Project のみ。作成は <c>projects.create</c>。</para>
/// <para>PATCH は <c>project.admin</c>。Project 本体の DELETE は提供しない。</para>
/// </remarks>
/// <param name="projects">Project ユースケース。</param>
[ApiController]
[Route("v1/projects")]
public sealed class ProjectsController(IProjectService projects) : ControllerBase
{
    /// <summary>GET /v1/projects — 所属 Project 一覧。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> List(CancellationToken ct) =>
        Ok(await projects.ListAsync(ct).ConfigureAwait(false));

    /// <summary>GET /v1/projects/{projectId} — メンバーであれば取得。未所属は 404。</summary>
    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectResponse>> Get(Guid projectId, CancellationToken ct) =>
        Ok(await projects.GetAsync(projectId, ct).ConfigureAwait(false));

    /// <summary>POST /v1/projects — 作成し、作成者を admin にする。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectResponse>> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken ct)
    {
        var created = await projects.CreateAsync(request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(Get), new { projectId = created.ProjectId }, created);
    }

    /// <summary>PATCH /v1/projects/{projectId} — 表示名・説明・slug の部分更新。</summary>
    [HttpPatch("{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectResponse>> Update(
        Guid projectId,
        [FromBody] UpdateProjectRequest request,
        CancellationToken ct) =>
        Ok(await projects.UpdateAsync(projectId, request, ct).ConfigureAwait(false));
}
