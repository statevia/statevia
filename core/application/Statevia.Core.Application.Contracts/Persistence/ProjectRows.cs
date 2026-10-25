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

/// <summary>メンバー画面用の主体。表示は <see cref="DisplayName"/>。Principal UUID は API 識別子。</summary>
public sealed class ProjectPrincipalLabel
{
    public Guid PrincipalId { get; set; }

    /// <summary>ユーザー表示名または API キー名。</summary>
    public string DisplayName { get; set; } = "";

    public PrincipalType PrincipalType { get; set; }
}

/// <summary>Project メンバーと表示名。</summary>
public sealed class ProjectMemberDetail
{
    public Guid PrincipalId { get; set; }

    public string DisplayName { get; set; } = "";

    public PrincipalType PrincipalType { get; set; }

    public ProjectAccessRole Role { get; set; }

    public DateTime CreatedAt { get; set; }
}
