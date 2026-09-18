using Xunit;

namespace Statevia.Tools.Capacity.Tests;

/// <summary>同梱 YAML が埋め込みリソースとして読めることを検証する。</summary>
public sealed class ScenarioDefinitionsTests
{
    /// <summary>L1 YAML に builtin noop が含まれる。</summary>
    [Fact]
    public void ReadYaml_L1_ContainsNoopAction()
    {
        var yaml = ScenarioDefinitions.ReadYaml(ScenarioDefinitions.L1ResourceName);

        Assert.Contains("statevia.action.builtin.execution.noop", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("type: wait", yaml, StringComparison.Ordinal);
    }

    /// <summary>L1O YAML に builtin sleep 500ms が含まれる。</summary>
    [Fact]
    public void ReadYaml_L1Occupied_ContainsSleepAction()
    {
        var yaml = ScenarioDefinitions.ReadYaml(ScenarioDefinitions.L1OccupiedResourceName);

        Assert.Contains("statevia.action.builtin.execution.sleep", yaml, StringComparison.Ordinal);
        Assert.Contains("duration: 500ms", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("statevia.action.builtin.execution.noop", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("type: wait", yaml, StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromMilliseconds(500), ScenarioDefinitions.L1OccupiedSleep);
    }

    /// <summary>L2 YAML に単一イベント go がある。</summary>
    [Fact]
    public void ReadYaml_L2_ContainsGoEvent()
    {
        var yaml = ScenarioDefinitions.ReadYaml(ScenarioDefinitions.L2ResourceName);

        Assert.Contains("go:", yaml, StringComparison.Ordinal);
        Assert.Contains("type: wait", yaml, StringComparison.Ordinal);
    }

    /// <summary>L3 YAML に timeout と go がある（現行では timeout 未使用）。</summary>
    [Fact]
    public void ReadYaml_L3_ContainsTimeoutAndGoEvent()
    {
        var yaml = ScenarioDefinitions.ReadYaml(ScenarioDefinitions.L3ResourceName);

        Assert.Contains("timeout: PT2S", yaml, StringComparison.Ordinal);
        Assert.Contains("go:", yaml, StringComparison.Ordinal);
        Assert.Contains("type: wait", yaml, StringComparison.Ordinal);
    }
}
