using Xunit;

namespace Statevia.Tools.Capacity.Tests;

/// <summary>compose restart の引数組み立てとサービス名検証。</summary>
public sealed class ComposeRestartTests
{
    /// <summary>既定サービス名で compose restart になる。</summary>
    [Fact]
    public void BuildRestartArguments_DefaultService_IsComposeRestart()
    {
        var arguments = ComposeRestart.BuildRestartArguments(ComposeRestart.DefaultServiceName);

        Assert.Equal(["compose", "restart", "service-api"], arguments);
    }

    /// <summary>version プローブは compose version。</summary>
    [Fact]
    public void BuildVersionArguments_IsComposeVersion()
    {
        var arguments = ComposeRestart.BuildVersionArguments();

        Assert.Equal(["compose", "version"], arguments);
    }

    /// <summary>不正なサービス名は拒否する。</summary>
    [Fact]
    public void NormalizeServiceName_ShellMetacharacters_Throws()
    {
        Assert.Throws<ArgumentException>(() => ComposeRestart.NormalizeServiceName("service-api; rm -rf /"));
    }
}
