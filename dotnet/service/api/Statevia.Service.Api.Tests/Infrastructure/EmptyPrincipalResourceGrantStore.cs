namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>ユニットテスト用。許可行を持たず、既存 Start の追加制限を増やさない。</summary>
internal sealed class EmptyPrincipalResourceGrantStore : IPrincipalResourceGrantStore
{
    /// <inheritdoc />
    public Task<IReadOnlyList<PrincipalResourceGrantRow>> ListAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        _ = principalId;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<PrincipalResourceGrantRow>>([]);
    }

    /// <inheritdoc />
    public Task ReplaceAsync(
        Guid principalId,
        IReadOnlyCollection<Guid> projectIds,
        IReadOnlyCollection<Guid> definitionIds,
        CancellationToken cancellationToken)
    {
        _ = principalId;
        _ = projectIds;
        _ = definitionIds;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
