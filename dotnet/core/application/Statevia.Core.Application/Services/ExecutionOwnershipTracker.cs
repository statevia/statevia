using System.Collections.Concurrent;

namespace Statevia.Core.Application.Services;

/// <summary>
/// プロセス内の実行セッション所有（Worker ID + fencing 世代）を保持する。
/// </summary>
/// <remarks>
/// Worker が claim 後に登録し、ステップ checkpoint / heartbeat が世代一致更新に使う。
/// </remarks>
public sealed class ExecutionOwnershipTracker
{
    private readonly ConcurrentDictionary<Guid, OwnedSession> _sessions = new();

    /// <summary>所有セッションを登録する。</summary>
    /// <param name="executionId">Execution ID。登録済みなら上書きする。</param>
    /// <param name="workerId">所有する Worker の ID。</param>
    /// <param name="ownerGeneration">所有の世代。fencing に使う。</param>
    public void Set(Guid executionId, string workerId, long ownerGeneration) =>
        _sessions[executionId] = new OwnedSession(workerId, ownerGeneration);

    /// <summary>登録済み所有を取得する。</summary>
    /// <param name="executionId">Execution ID。</param>
    /// <returns>登録があれば <see langword="true"/>。</returns>
    /// <param name="workerId">所有する Worker の ID。登録が無ければ空文字。</param>
    /// <param name="ownerGeneration">所有の世代。登録が無ければ 0。</param>
    public bool TryGet(Guid executionId, out string workerId, out long ownerGeneration)
    {
        if (_sessions.TryGetValue(executionId, out var session))
        {
            workerId = session.WorkerId;
            ownerGeneration = session.OwnerGeneration;
            return true;
        }

        workerId = string.Empty;
        ownerGeneration = 0;
        return false;
    }

    /// <summary>所有登録を削除する。</summary>
    /// <param name="executionId">Execution ID。登録が無ければ何もしない。</param>
    public void Clear(Guid executionId) => _sessions.TryRemove(executionId, out _);

    private sealed record OwnedSession(string WorkerId, long OwnerGeneration);
}
