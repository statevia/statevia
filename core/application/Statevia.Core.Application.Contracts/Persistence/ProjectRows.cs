using Statevia.Core.Application.Contracts.Security;

namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>projects テーブル。</summary>
public sealed class ProjectRow
{
    public Guid ProjectId { get; set; }

    /// <summary>所属テナント内部 UUID（tenants.tenant_id）。</summary>
    public Guid TenantId { get; set; }

    /// <summary>テナント内 slug。</summary>
    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>project_accesses テーブル — 認可 truth。</summary>
public sealed class ProjectAccessRow
{
    public Guid ProjectId { get; set; }

    /// <summary>付与先 Principal。</summary>
    public Guid PrincipalId { get; set; }

    public ProjectAccessRole Role { get; set; }

    public DateTime CreatedAt { get; set; }
}
