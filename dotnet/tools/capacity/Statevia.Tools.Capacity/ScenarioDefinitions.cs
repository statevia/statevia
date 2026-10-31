using System.Reflection;

namespace Statevia.Tools.Capacity;

/// <summary>同梱 Definition YAML を埋め込みリソースから読む。</summary>
internal static class ScenarioDefinitions
{
    internal const string L1ResourceName = "Statevia.Tools.Capacity.Definitions.l1-short-lived.yaml";
    internal const string L1OccupiedResourceName = "Statevia.Tools.Capacity.Definitions.l1-occupied-sleep.yaml";
    internal const string L2ResourceName = "Statevia.Tools.Capacity.Definitions.l2-event-wait.yaml";
    internal const string L3ResourceName = "Statevia.Tools.Capacity.Definitions.l3-delay-wait.yaml";
    internal const string L2ResumeEvent = "go";

    /// <summary>L1O の sleep 占有時間。スロットを握るが CPU は焼かない。</summary>
    internal static readonly TimeSpan L1OccupiedSleep = TimeSpan.FromMilliseconds(500);

    /// <summary>L3 YAML の timeout（現行 Loader では未使用）。観測窓の目安。</summary>
    internal static readonly TimeSpan L3YamlTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Wait 発見後に TimerFire を待つ秒数（Scheduler poll 5s × 3）。</summary>
    internal const int L3ObserveSecondsAfterWait = 15;

    /// <summary>指定リソース名の YAML 本文を返す。</summary>
    /// <param name="resourceName">埋め込み名。</param>
    /// <returns>YAML 文字列。</returns>
    /// <exception cref="InvalidOperationException">リソースが無い。</exception>
    public static string ReadYaml(string resourceName)
    {
        var assembly = typeof(ScenarioDefinitions).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded definition '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
