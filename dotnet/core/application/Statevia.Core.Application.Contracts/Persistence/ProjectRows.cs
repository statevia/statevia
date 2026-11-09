using Statevia.Core.Application.Contracts.Security;

namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>projects テーブル。</summary>
public sealed class ProjectRow
{
    /// <summary>Project ID。</summary>
    public Guid ProjectId { get; set; }

    /// <summary>所属テナント内部 UUID（tenants.tenant_id）。</summary>
    public Guid TenantId { get; set; }

    /// <summary>テナント内 slug。</summary>
    public required string Slug { get; set; }

    /// <summary>表示名。</summary>
    public required string DisplayName { get; set; }

    /// <summary>説明。無ければ null。</summary>
    public string? Description { get; set; }

    /// <summary>作成日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>project_accesses テーブル — 認可 truth。</summary>
public sealed class ProjectAccessRow
{
    /// <summary>対象の Project ID。</summary>
    public Guid ProjectId { get; set; }

    /// <summary>付与先 Principal。</summary>
    public Guid PrincipalId { get; set; }

    /// <summary>Project のロール（reader / executor / publisher / admin）。</summary>
    public ProjectAccessRole Role { get; set; }

    /// <summary>付与日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>メンバー画面用の主体。表示は <see cref="DisplayName"/>。Principal UUID は API 識別子。</summary>
public sealed class ProjectPrincipalLabel
{
    /// <summary>Principal ID。</summary>
    public Guid PrincipalId { get; set; }

    /// <summary>ユーザー表示名または API キー名。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Principal の種別。</summary>
    public PrincipalType PrincipalType { get; set; }
}

/// <summary>Project メンバーと表示名。</summary>
public sealed class ProjectMemberDetail
{
    /// <summary>Principal ID。</summary>
    public Guid PrincipalId { get; set; }

    /// <summary>表示名（ユーザー表示名または API キー名）。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Principal の種別。</summary>
    public PrincipalType PrincipalType { get; set; }

    /// <summary>Project のロール。</summary>
    public ProjectAccessRole Role { get; set; }

    /// <summary>付与日時（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}
