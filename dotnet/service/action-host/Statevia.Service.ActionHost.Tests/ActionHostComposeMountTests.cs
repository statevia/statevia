namespace Statevia.Service.ActionHost.Tests;

/// <summary>製品 compose の modules マウントが Action Host だけ読み取り専用であることを固定する。</summary>
public sealed class ActionHostComposeMountTests
{
    /// <summary>action-host の modules は :ro で、service-api は配置のため読み書きのままである。</summary>
    [Fact]
    public void Compose_ActionHostModulesMount_IsReadOnly_AndApiRemainsReadWrite()
    {
        // Arrange
        var compose = File.ReadAllText(FindRepoFile("docker-compose.yml"));
        var api = SliceService(compose, "service-api");
        var actionHost1 = SliceService(compose, "action-host-1");
        var actionHost2 = SliceService(compose, "action-host-2");

        // Act
        var apiModulesLine = FindModulesMount(api);
        var actionHost1ModulesLine = FindModulesMount(actionHost1);
        var actionHost2ModulesLine = FindModulesMount(actionHost2);

        // Assert
        Assert.Equal("./modules:/app/modules", apiModulesLine);
        Assert.Equal("./modules:/app/modules:ro", actionHost1ModulesLine);
        Assert.Equal("./modules:/app/modules:ro", actionHost2ModulesLine);
    }

    private static string FindModulesMount(string serviceBlock)
    {
        foreach (var line in serviceBlock.Split('\n'))
        {
            var trimmed = line.Trim().TrimEnd('\r');
            if (trimmed.StartsWith("- ./modules:/app/modules", StringComparison.Ordinal))
            {
                return trimmed["- ".Length..];
            }
        }

        throw new InvalidOperationException("modules mount was not found.");
    }

    private static string SliceService(string compose, string serviceName)
    {
        var lines = compose.Split('\n');
        var buffer = new List<string>();
        var capturing = false;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            var isServiceHeader = line.StartsWith("  ", StringComparison.Ordinal)
                && !line.StartsWith("   ", StringComparison.Ordinal)
                && line.EndsWith(':');
            if (isServiceHeader)
            {
                if (capturing)
                {
                    break;
                }

                capturing = line == $"  {serviceName}:";
            }

            if (capturing)
            {
                buffer.Add(line);
            }
        }

        if (buffer.Count == 0)
        {
            throw new InvalidOperationException($"Service {serviceName} was not found.");
        }

        return string.Join('\n', buffer);
    }

    private static string FindRepoFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate)
                && File.Exists(Path.Combine(directory.FullName, "statevia.sln")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"{fileName} was not found from the test output directory.");
    }
}
