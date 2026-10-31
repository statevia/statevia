namespace Statevia.Service.ActionHost.Tests;

/// <summary><see cref="ActionHostProcessEnvironment"/> の起動前判定。</summary>
public sealed class ActionHostProcessEnvironmentTests
{
    private const string SecretValue = "super-secret-db-password";

    /// <summary>禁止名に空白以外の値があるときは、値を含めずに起動失敗にする。</summary>
    [Fact]
    public void CreatePlan_WhenDeniedNameHasValue_RefusesWithoutValue()
    {
        // Arrange
        var variables = new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = SecretValue,
            ["STATEVIA_MODULES_PATH"] = "/app/modules",
        };

        // Act
        var plan = ActionHostProcessEnvironment.CreatePlan(variables);
        var message = "Action Host refused to start because these environment variables are set: "
            + string.Join(", ", plan.RefusedNames);

        // Assert
        Assert.Equal(["DATABASE_URL"], plan.RefusedNames);
        Assert.Contains("DATABASE_URL", message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretValue, message, StringComparison.Ordinal);
    }

    /// <summary>禁止名が空白だけ、または未設定のときは拒否理由にしない。</summary>
    [Fact]
    public void CreatePlan_WhenDeniedNameIsBlank_DoesNotRefuse()
    {
        // Arrange
        var variables = new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = "   ",
            ["STATEVIA_SECRETS_KEY"] = "",
            ["ASPNETCORE_URLS"] = "http://+:5001",
        };

        // Act
        var plan = ActionHostProcessEnvironment.CreatePlan(variables);

        // Assert
        Assert.Empty(plan.RefusedNames);
        Assert.Contains("DATABASE_URL", plan.NamesToRemove);
        Assert.Contains("STATEVIA_SECRETS_KEY", plan.NamesToRemove);
        Assert.DoesNotContain("ASPNETCORE_URLS", plan.NamesToRemove);
    }

    /// <summary>許可リスト外は削除し、Module パスと待受 URL は残す。</summary>
    [Fact]
    public void CreatePlan_WhenNameIsOutsideAllowList_RemovesItAndKeepsAllowList()
    {
        // Arrange
        var variables = new Dictionary<string, string?>
        {
            ["STATEVIA_MODULES_PATH"] = "/app/modules",
            ["ASPNETCORE_URLS"] = "http://+:5001",
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["COMPlus_ReadyToRun"] = "0",
            ["CUSTOM_PROBE"] = "keep-me-out",
            ["Path"] = "/usr/bin",
        };

        // Act
        var plan = ActionHostProcessEnvironment.CreatePlan(variables);

        // Assert
        Assert.Empty(plan.RefusedNames);
        Assert.Equal(["CUSTOM_PROBE"], plan.NamesToRemove);
        Assert.DoesNotContain(plan.NamesToRemove, name => name is "STATEVIA_MODULES_PATH" or "ASPNETCORE_URLS" or "Path");
    }
}
