namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>ユニットテスト用。渡した許可行のうち、指定 Principal の行だけを返す。</summary>
/// <param name="rows">返す許可行。</param>
internal sealed class FixedPrincipalResourceGrantStore(IReadOnlyList<PrincipalResourceGrantRow> rows)
    : IPrincipalResourceGrantStore
{
    /// <inheritdoc />
    public Task<IReadOnlyList<PrincipalResourceGrantRow>> ListAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        IReadOnlyList<PrincipalResourceGrantRow> matched = rows
            .Where(row => row.PrincipalId == principalId)
            .ToList();
        return Task.FromResult(matched);
    }

    /// <inheritdoc />
    public Task ReplaceAsync(
        Guid principalId,
        IReadOnlyCollection<Guid> projectIds,
        IReadOnlyCollection<Guid> definitionIds,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
