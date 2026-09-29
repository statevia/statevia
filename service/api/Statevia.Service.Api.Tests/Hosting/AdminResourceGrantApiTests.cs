using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Contracts.Admin;
using Statevia.Service.Api.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>実行リソース許可の管理 API。</summary>
public sealed class AdminResourceGrantApiTests : IClassFixture<SecurityIntegrationWebApplicationFactory>
{
    private readonly SecurityIntegrationWebApplicationFactory _factory;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public AdminResourceGrantApiTests(SecurityIntegrationWebApplicationFactory factory) =>
        _factory = factory;

    /// <summary>非管理者はユーザーの許可を置き換えられない。</summary>
    [Fact]
    public async Task ReplaceUserResourceGrants_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("grant-member@example.com", "password", isTenantAdmin: false);
        using var client = CreateClient(principalId);
        var userId = await FindUserIdAsync(principalId);

        // Act
        var response = await PutGrantsAsync(client, $"/v1/admin/users/{userId}/resource-grants", [], []);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>存在しないユーザーは 404。</summary>
    [Fact]
    public async Task GetUserResourceGrants_UnknownUser_ReturnsNotFound()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("grant-admin-missing@example.com", "password", isTenantAdmin: true);
        using var client = CreateClient(principalId);

        // Act
        var response = await client.GetAsync(
            new Uri($"/v1/admin/users/{Guid.NewGuid()}/resource-grants", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>他テナントのユーザーは 404。</summary>
    [Fact]
    public async Task GetUserResourceGrants_OtherTenantUser_ReturnsNotFound()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("grant-admin-other@example.com", "password", isTenantAdmin: true);
        using var client = CreateClient(principalId);
        var otherUserId = await SeedUserAsync(TestTenantIds.T1TenantId);

        // Act
        var response = await client.GetAsync(
            new Uri($"/v1/admin/users/{otherUserId}/resource-grants", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>管理者はユーザーへ project と definition の許可を付けられる。</summary>
    [Fact]
    public async Task ReplaceUserResourceGrants_ExecutableResources_ReturnsThem()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("grant-admin-set@example.com", "password", isTenantAdmin: true);
        using var client = CreateClient(principalId);
        var userId = await CreateUserIdAsync(client);
        var projectId = await SeedProjectAsync(TestTenantIds.DefaultTenantId, accessForDefault: null);
        var definitionId = await SeedDefinitionAsync(projectId, TestTenantIds.DefaultTenantId);

        // Act
        var response = await PutGrantsAsync(
            client,
            $"/v1/admin/users/{userId}/resource-grants",
            [projectId],
            [definitionId]);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PrincipalResourceGrantsDto>();
        Assert.NotNull(body);
        Assert.Equal([projectId], body!.ProjectIds);
        Assert.Equal([definitionId], body.DefinitionIds);
    }

    /// <summary>空配列は両方の許可を消す。</summary>
    [Fact]
    public async Task ReplaceUserResourceGrants_EmptyArrays_ClearsGrants()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("grant-admin-clear@example.com", "password", isTenantAdmin: true);
        using var client = CreateClient(principalId);
        var userId = await CreateUserIdAsync(client);
        var projectId = await SeedProjectAsync(TestTenantIds.DefaultTenantId, accessForDefault: null);
        var put = await PutGrantsAsync(client, $"/v1/admin/users/{userId}/resource-grants", [projectId], []);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        // Act
        var cleared = await PutGrantsAsync(client, $"/v1/admin/users/{userId}/resource-grants", [], []);

        // Assert
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var body = await client.GetFromJsonAsync<PrincipalResourceGrantsDto>(
            new Uri($"/v1/admin/users/{userId}/resource-grants", UriKind.Relative));
        Assert.NotNull(body);
        Assert.Empty(body!.ProjectIds);
        Assert.Empty(body.DefinitionIds);
    }

    /// <summary>executor 未満の project は保存せず 422。</summary>
    [Fact]
    public async Task ReplaceUserResourceGrants_ReaderProject_ReturnsUnprocessableEntity()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("grant-admin-reader@example.com", "password", isTenantAdmin: true);
        using var client = CreateClient(principalId);
        var userId = await CreateUserIdAsync(client);
        var projectId = await SeedProjectAsync(TestTenantIds.T1TenantId, ProjectAccessRole.Reader);

        // Act
        var response = await PutGrantsAsync(
            client,
            $"/v1/admin/users/{userId}/resource-grants",
            [projectId],
            []);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await client.GetFromJsonAsync<PrincipalResourceGrantsDto>(
            new Uri($"/v1/admin/users/{userId}/resource-grants", UriKind.Relative));
        Assert.NotNull(body);
        Assert.Empty(body!.ProjectIds);
    }

    /// <summary>実行できない project の定義は保存せず 422。</summary>
    [Fact]
    public async Task ReplaceUserResourceGrants_DefinitionOnReaderProject_ReturnsUnprocessableEntity()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("grant-admin-def@example.com", "password", isTenantAdmin: true);
        using var client = CreateClient(principalId);
        var userId = await CreateUserIdAsync(client);
        var projectId = await SeedProjectAsync(TestTenantIds.T1TenantId, ProjectAccessRole.Reader);
        var definitionId = await SeedDefinitionAsync(projectId, TestTenantIds.DefaultTenantId);

        // Act
        var response = await PutGrantsAsync(
            client,
            $"/v1/admin/users/{userId}/resource-grants",
            [],
            [definitionId]);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>配列を省略した PUT は 422。</summary>
    [Fact]
    public async Task ReplaceUserResourceGrants_MissingProjectIds_ReturnsUnprocessableEntity()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("grant-admin-omit@example.com", "password", isTenantAdmin: true);
        using var client = CreateClient(principalId);
        var userId = await CreateUserIdAsync(client);

        // Act
        var response = await client.PutAsJsonAsync(
            new Uri($"/v1/admin/users/{userId}/resource-grants", UriKind.Relative),
            new { definitionIds = Array.Empty<Guid>() });

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>project 許可が上限を超えると 422。</summary>
    [Fact]
    public async Task ReplaceUserResourceGrants_TooManyProjects_ReturnsUnprocessableEntity()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("grant-admin-limit@example.com", "password", isTenantAdmin: true);
        using var client = CreateClient(principalId);
        var userId = await CreateUserIdAsync(client);
        var projectIds = Enumerable.Range(0, PrincipalResourceGrantLimits.MaxProjects + 1).Select(_ => Guid.NewGuid()).ToArray();

        // Act
        var response = await PutGrantsAsync(
            client,
            $"/v1/admin/users/{userId}/resource-grants",
            projectIds,
            []);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>管理者は ServiceAccount へも許可を付けられる。</summary>
    [Fact]
    public async Task ReplaceServiceAccountResourceGrants_ExecutableProject_ReturnsIt()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("grant-admin-sa@example.com", "password", isTenantAdmin: true);
        using var client = CreateClient(principalId);
        var groupResponse = await client.PostAsJsonAsync(
            new Uri("/v1/admin/groups", UriKind.Relative),
            new CreateAdminGroupRequest { Name = $"grant-{Guid.NewGuid():N}" });
        var group = await groupResponse.Content.ReadFromJsonAsync<AdminGroupDetailDto>();
        Assert.NotNull(group);
        var createResponse = await client.PostAsJsonAsync(
            new Uri("/v1/admin/service-accounts", UriKind.Relative),
            new CreateAdminServiceAccountRequest
            {
                Name = $"r{Guid.NewGuid():N}"[..16],
                GroupIds = [group!.GroupId]
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminServiceAccountListItemDto>();
        Assert.NotNull(created);
        var projectId = await SeedProjectAsync(TestTenantIds.DefaultTenantId, accessForDefault: null);

        // Act
        var response = await PutGrantsAsync(
            client,
            $"/v1/admin/service-accounts/{created!.ServiceAccountId}/resource-grants",
            [projectId],
            []);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PrincipalResourceGrantsDto>();
        Assert.NotNull(body);
        Assert.Equal([projectId], body!.ProjectIds);
        Assert.Empty(body.DefinitionIds);
    }

    private HttpClient CreateClient(Guid principalId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _factory.IssueBearerToken(principalId));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");
        return client;
    }

    private static async Task<Guid> CreateUserIdAsync(HttpClient client)
    {
        var username = $"u{Guid.NewGuid():N}"[..16];
        var response = await client.PostAsJsonAsync(
            new Uri("/v1/admin/users", UriKind.Relative),
            new { username, password = "password" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AdminUserListItemDto>();
        Assert.NotNull(created);
        return created!.UserId;
    }

    private static Task<HttpResponseMessage> PutGrantsAsync(
        HttpClient client,
        string path,
        IReadOnlyList<Guid> projectIds,
        IReadOnlyList<Guid> definitionIds) =>
        client.PutAsJsonAsync(
            new Uri(path, UriKind.Relative),
            new ReplacePrincipalResourceGrantsRequest
            {
                ProjectIds = projectIds,
                DefinitionIds = definitionIds
            });

    private async Task<Guid> SeedUserAsync(Guid tenantId)
    {
        var userId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = "other",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Users.Add(new UserRow
        {
            UserId = userId,
            TenantId = tenantId,
            Username = $"o{userId:N}"[..16],
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UserPrincipals.Add(new UserPrincipalRow { UserId = userId, PrincipalId = principalId });
        await db.SaveChangesAsync();
        return userId;
    }

    private async Task<Guid> FindUserIdAsync(Guid principalId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var link = await db.UserPrincipals.AsNoTracking().FirstAsync(row => row.PrincipalId == principalId);
        return link.UserId;
    }

    private async Task<Guid> SeedProjectAsync(Guid ownerTenantId, ProjectAccessRole? accessForDefault)
    {
        var projectId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Projects.Add(new ProjectRow
        {
            ProjectId = projectId,
            OwnerTenantId = ownerTenantId,
            Slug = $"p{projectId:N}"[..16],
            DisplayName = "grant",
            Visibility = ProjectVisibility.Private,
            CreatedAt = DateTime.UtcNow
        });
        if (accessForDefault is { } role)
            ProjectTestData.GrantAccess(db, projectId, TestTenantIds.DefaultTenantId, role);
        await db.SaveChangesAsync();
        return projectId;
    }

    private async Task<Guid> SeedDefinitionAsync(Guid projectId, Guid tenantId)
    {
        var definitionId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CoreDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        DefinitionTestData.AddDefinitionWithVersion(db, tenantId, definitionId, $"d{definitionId:N}"[..12], projectId);
        await db.SaveChangesAsync();
        return definitionId;
    }
}
