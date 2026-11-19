namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>
/// シナリオテストの前提（Docker 上の PostgreSQL）が満たせないときに、Skipped にするか失敗にするかを決める。
/// </summary>
/// <remarks>
/// <para>ローカルは Docker が無くても開発を止めないよう Skipped にする。</para>
/// <para>環境変数 <see cref="EnvironmentVariableName"/> が <c>true</c> のとき（CI）は失敗にし、必須ゲートを空にしない。</para>
/// </remarks>
public static class ScenarioRequirement
{
    /// <summary>フィクスチャの起動失敗を Skipped ではなく失敗にする環境変数の名前。値が <c>true</c> のときだけ有効。</summary>
    public const string EnvironmentVariableName = "STATEVIA_REQUIRE_SCENARIO_TESTS";

    /// <summary>起動失敗を失敗にするか（環境変数が <c>true</c>）。</summary>
    public static bool IsRequired =>
        string.Equals(
            Environment.GetEnvironmentVariable(EnvironmentVariableName),
            "true",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>前提が満たせないとき、必須指定なら失敗、そうでなければ Skipped にする。</summary>
    /// <param name="isReady">フィクスチャが利用可能か。</param>
    /// <param name="reason">利用できない理由。</param>
    public static void SkipOrFailIfNotReady(bool isReady, string? reason)
    {
        var message = reason ?? "Docker が利用できません。";
        if (!isReady && IsRequired)
            Assert.Fail($"{EnvironmentVariableName}=true のため、スキップせず失敗にします。{message}");

        Skip.IfNot(isReady, message);
    }
}
