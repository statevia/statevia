using Statevia.Core.Application.Contracts.Services;
using System.Text.Json;

namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <see cref="ExecutionWorkItemKinds.Start"/> の JSON ペイロード。
/// </summary>
/// <remarks>
/// HTTP Start が実行行を先に作成したうえで enqueue する。
/// Worker は <see cref="ExecutionId"/> を正として Engine を起動する。
/// </remarks>
/// <param name="ExecutionId">受理済み実行 ID。</param>
/// <param name="Request">開始リクエスト（定義・入力）。</param>
/// <param name="ForkBranch">
/// Hosted Fork の枝（子 execution）の開始のときだけ設定する。HTTP 由来の開始では null。
/// 公開 API の <see cref="StartExecutionRequest"/> には載せず、内部の work item だけで運ぶ。
/// </param>
public sealed record ExecutionStartWorkItemPayload(
    Guid ExecutionId,
    StartExecutionRequest Request,
    ForkBranchStartPayload? ForkBranch = null);

/// <summary>
/// Start work item で運ぶ、Fork の枝としての開始指定。
/// </summary>
/// <remarks>
/// Worker はこれを <see cref="Statevia.Core.Engine.Abstractions.ForkBranchStart"/> にして Engine を開始する。
/// 枝の先頭の input（<see cref="StartExecutionRequest.Input"/>）は親で評価済みとして扱われる。
/// </remarks>
/// <param name="WorkflowInput">親のワークフロー開始 input。子の <c>$.input</c> になる。</param>
public sealed record ForkBranchStartPayload(JsonElement? WorkflowInput);
