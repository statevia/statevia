using Statevia.Core.Application.Contracts.Validation;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Statevia.Core.Application.Contracts.Services;

/// <summary>テナント内 Project の作成・取得・更新・所属一覧。</summary>
public interface IProjectService
{
    /// <summary>呼び出し元 Principal がメンバーの Project だけを返す。</summary>
    Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken ct);

    /// <summary>メンバーであれば取得する。未所属は 404。</summary>
    Task<ProjectResponse> GetAsync(Guid projectId, CancellationToken ct);

    /// <summary><c>projects.create</c> で作成し、作成者を <c>admin</c> にする。</summary>
    Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken ct);

    /// <summary><c>project.admin</c> が表示名・説明・slug を部分更新する。</summary>
    Task<ProjectResponse> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct);

    /// <summary><c>project.admin</c> または <c>tenant.admin</c> がメンバーを一覧する。</summary>
    Task<IReadOnlyList<ProjectMemberResponse>> ListMembersAsync(Guid projectId, CancellationToken ct);

    /// <summary>メンバーを付与またはロール変更する。自己昇格を含む。</summary>
    Task UpsertMemberAsync(
        Guid projectId,
        Guid principalId,
        UpsertProjectMemberRequest request,
        CancellationToken ct);

    /// <summary>メンバーを外す。最後の <c>admin</c> は <c>tenant.admin</c> だけが外せる。</summary>
    Task RemoveMemberAsync(Guid projectId, Guid principalId, CancellationToken ct);

    /// <summary><c>tenant.admin</c> がテナント内の全 Project を見る。未所属を含む。</summary>
    Task<IReadOnlyList<ProjectResponse>> ListAllForTenantAdminAsync(CancellationToken ct);
}

/// <summary>POST /v1/projects の本文。</summary>
public sealed class CreateProjectRequest
{
    /// <summary>テナント内 slug。</summary>
    [Required(ErrorMessage = "slug is required")]
    [NotWhitespace(ErrorMessage = "slug is required")]
    [MaxLength(128)]
    public string Slug { get; set; } = "";

    /// <summary>表示名。</summary>
    [Required(ErrorMessage = "displayName is required")]
    [NotWhitespace(ErrorMessage = "displayName is required")]
    [MaxLength(256)]
    public string DisplayName { get; set; } = "";

    /// <summary>説明（任意）。</summary>
    public string? Description { get; set; }
}

/// <summary>PATCH /v1/projects/{id} の本文。省略した項目は維持する。</summary>
public sealed class UpdateProjectRequest : IValidatableObject
{
    /// <summary>表示名。</summary>
    [MaxLength(256)]
    public string? DisplayName { get; set; }

    /// <summary>説明。指定時のみ上書きする（省略時は維持）。</summary>
    public string? Description { get; set; }

    /// <summary>テナント内 slug。</summary>
    [MaxLength(128)]
    public string? Slug { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DisplayName is { } displayName && string.IsNullOrWhiteSpace(displayName))
            yield return new ValidationResult("displayName is required", [nameof(DisplayName)]);

        if (Slug is { } slug && string.IsNullOrWhiteSpace(slug))
            yield return new ValidationResult("slug is required", [nameof(Slug)]);
    }
}

/// <summary>Project 応答。</summary>
public sealed class ProjectResponse
{
    /// <summary>Project UUID。</summary>
    [JsonPropertyName("projectId")]
    public Guid ProjectId { get; set; }

    /// <summary>テナント内 slug。</summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    /// <summary>表示名。</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    /// <summary>説明。</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>作成日時（UTC）。</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>呼び出し元の実効ロール（storage 文字列）。未所属の管理一覧では null。</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

/// <summary>Project メンバー 1 件。</summary>
public sealed class ProjectMemberResponse
{
    /// <summary>付与先 Principal。</summary>
    [JsonPropertyName("principalId")]
    public Guid PrincipalId { get; set; }

    /// <summary>実効ロール（storage 文字列）。</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    /// <summary>付与日時（UTC）。</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>PUT /v1/projects/{id}/members/{principalId} の本文。</summary>
public sealed class UpsertProjectMemberRequest
{
    /// <summary><c>reader</c> / <c>executor</c> / <c>publisher</c> / <c>admin</c>。</summary>
    [Required(ErrorMessage = "role is required")]
    [NotWhitespace(ErrorMessage = "role is required")]
    public string Role { get; set; } = "";
}
