using Statevia.Infrastructure.Persistence;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>待機中の判定と件数のテスト用に、定義・実行・Wait・物理子の関係を投入する（SQLite の既定テナント）。</summary>
internal sealed class ExecutionWaitingTestData(CoreDbContext ctx)
{
    /// <summary>既定テナント。</summary>
    public static readonly Guid TenantId = TestTenantIds.DefaultTenantId;

    /// <summary>投入した定義の初版 ID（定義 ID ごと）。</summary>
    private readonly Dictionary<Guid, Guid> _versionIds = [];

    /// <summary>Project と、その Project の Reader 以上のメンバーを作り、メンバーの Principal ID を返す。</summary>
    /// <param name="projectId">Project ID。</param>
    /// <returns>Principal ID。</returns>
    public Guid AddProjectWithMember(Guid projectId)
    {
        AddProject(projectId);
        return ProjectTestData.AddMemberPrincipal(ctx, TenantId, projectId);
    }

    /// <summary>Project を作る（メンバーは作らない）。スラッグは Project ごとに変える。</summary>
    /// <param name="projectId">Project ID。</param>
    public void AddProject(Guid projectId) =>
        ctx.Projects.Add(new ProjectRow
        {
            ProjectId = projectId,
            TenantId = TenantId,
            Slug = $"p-{projectId:N}"[..20],
            DisplayName = "project",
            CreatedAt = DateTime.UtcNow
        });

    /// <summary>定義と初版を作る。</summary>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="projectId">所属 Project。</param>
    public void AddDefinition(Guid definitionId, Guid projectId)
    {
        var (_, version) = DefinitionTestData.AddDefinitionWithVersion(ctx, TenantId, definitionId, $"def-{definitionId:N}", projectId);
        _versionIds[definitionId] = version.DefinitionVersionId;
    }

    /// <summary>実行を作る。</summary>
    /// <param name="definitionId">定義 ID（先に <see cref="AddDefinition"/> で作ること）。</param>
    /// <param name="status">投影の値。</param>
    /// <param name="startedAt">開始日時。</param>
    /// <param name="displayId">表示 ID。省略時は採番しない。</param>
    /// <returns>実行 ID。</returns>
    public Guid AddExecution(Guid definitionId, string status, DateTime startedAt, string? displayId = null)
    {
        var executionId = Guid.NewGuid();
        ctx.Executions.Add(new ExecutionRow
        {
            ExecutionId = executionId,
            TenantId = TenantId,
            DefinitionId = definitionId,
            DefinitionVersionId = _versionIds[definitionId],
            Status = status,
            StartedAt = startedAt,
            UpdatedAt = startedAt,
            CancelRequested = false,
            RestartLost = false
        });
        if (displayId is not null)
        {
            ctx.DisplayIds.Add(new DisplayIdRow
            {
                Kind = "execution",
                DisplayId = displayId,
                ResourceId = executionId,
                CreatedAt = startedAt
            });
        }

        return executionId;
    }

    /// <summary>実行に Wait を足す。</summary>
    /// <param name="executionId">実行 ID。</param>
    /// <param name="waitKind">Wait の種類。</param>
    public void AddWait(Guid executionId, ExecutionWaitKind waitKind) =>
        ctx.ExecutionWaits.Add(new ExecutionWaitRow
        {
            ExecutionId = executionId,
            NodeId = $"n-{Guid.NewGuid():N}"[..12],
            WaitKind = waitKind,
            AllowedEvents = ["approve"],
            CreatedAt = DateTime.UtcNow
        });

    /// <summary>親子（物理子 Execution）の関係を足す。</summary>
    /// <param name="parentExecutionId">親の実行 ID。</param>
    /// <param name="childExecutionId">子の実行 ID。</param>
    public void AddBranch(Guid parentExecutionId, Guid childExecutionId) =>
        ctx.ExecutionBranches.Add(new ExecutionBranchRow
        {
            ParentExecutionId = parentExecutionId,
            ExecutionId = childExecutionId,
            ForkNodeId = $"f-{Guid.NewGuid():N}"[..12],
            JoinState = "join",
            BranchState = "branch",
            Status = ExecutionBranchStatuses.Running,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
}
