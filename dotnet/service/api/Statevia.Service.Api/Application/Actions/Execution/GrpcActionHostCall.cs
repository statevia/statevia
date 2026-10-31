using Grpc.Net.Client;
using Statevia.Core.Actions.Abstractions.Execution;
using Statevia.Infrastructure.Actions.Grpc.Contracts;

namespace Statevia.Service.Api.Application.Actions.Execution;

/// <summary>1 つの絶対 URI に対する gRPC チャネル。</summary>
internal sealed class GrpcActionHostCall : IActionHostCall, IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly ActionExecutionService.ActionExecutionServiceClient _client;

    /// <summary>指定アドレスへのチャネルを開く。</summary>
    /// <param name="address">絶対 http(s) URI。</param>
    public GrpcActionHostCall(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        _channel = GrpcChannel.ForAddress(address);
        _client = new ActionExecutionService.ActionExecutionServiceClient(_channel);
    }

    /// <inheritdoc />
    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionRequest request,
        CancellationToken cancellationToken) =>
        ActionHostGrpcInvoker.ExecuteAsync(_client, request, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _channel.Dispose();
}
