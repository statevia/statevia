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
/// 親から引き継ぐ値を payload に永続化し、Worker の再起動後も同じ値で開始できるようにする。
/// </remarks>
/// <param name="WorkflowInput">親のワークフロー開始 input。子の <c>$.input</c> になる。</param>
/// <param name="Vars">Fork 到達時点の親の <c>vars</c>。子の <c>vars</c> の初期値になる。</param>
/// <param name="States">Fork 到達時点の親の <c>states</c>（状態名 → <c>{ output }</c>）。子の <c>states</c> の初期値になる。</param>
/// <param name="LogicalExecutionId">
/// 論理実行 ID（親の <c>$.sys.execution.id</c>）。子の <c>$.sys.execution.id</c> になる。
/// null（引き継ぎを運ぶ前に enqueue した payload）のときは子実行自身の ID。
/// </param>
public sealed record ForkBranchStartPayload(
    JsonElement? WorkflowInput,
    JsonElement? Vars,
    JsonElement? States,
    string? LogicalExecutionId);
