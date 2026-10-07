namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>統合テストのホスト起動時にだけ使う接続文字列。</summary>
/// <remarks>
/// 本番の <c>appsettings</c> には接続文字列を置かない。
/// セキュリティ統合テストは起動後に SQLite へ差し替える。OpenAPI のスモークは PostgreSQL を開かない。
/// </remarks>
internal static class TestConnectionStrings
{
    /// <summary>
    /// <see cref="Statevia.Infrastructure.Common.DatabaseConnection.Resolve"/> を通すための値。
    /// 実在する PostgreSQL を指すものではない。
    /// </summary>
    internal const string HostStartupOnly =
        "Host=localhost;Database=statevia;Username=statevia;Password=statevia";
}
