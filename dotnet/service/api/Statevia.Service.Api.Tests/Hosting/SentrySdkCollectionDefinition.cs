namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>
/// <see cref="Sentry.SentrySdk"/> はプロセス内で静的状態を持つため、初期化テストを直列化する。
/// </summary>
[CollectionDefinition("SentrySdk", DisableParallelization = true)]
public sealed class SentrySdkCollectionDefinition;
