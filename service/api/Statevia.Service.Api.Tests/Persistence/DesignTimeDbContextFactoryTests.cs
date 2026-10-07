using Statevia.Infrastructure.Persistence;

namespace Statevia.Service.Api.Tests.Persistence;

/// <summary><see cref="DesignTimeDbContextFactory"/> の検証。</summary>
public sealed class DesignTimeDbContextFactoryTests
{
    /// <summary>EF CLI 用ファクトリが DbContext を生成する。</summary>
    [Fact]
    public void CreateDbContext_ReturnsCoreDbContext()
    {
        // Arrange
        var previous = Environment.GetEnvironmentVariable("DATABASE_URL");
        Environment.SetEnvironmentVariable(
            "DATABASE_URL",
            "Host=localhost;Database=statevia;Username=statevia;Password=statevia");
        try
        {
            var factory = new DesignTimeDbContextFactory();

            // Act
            using var context = factory.CreateDbContext([]);

            // Assert
            Assert.NotNull(context);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", previous);
        }
    }

    /// <summary><c>DATABASE_URL</c> が無いとき例外にする。</summary>
    [Fact]
    public void CreateDbContext_ThrowsWhenDatabaseUrlMissing()
    {
        // Arrange
        var previous = Environment.GetEnvironmentVariable("DATABASE_URL");
        Environment.SetEnvironmentVariable("DATABASE_URL", null);
        try
        {
            var factory = new DesignTimeDbContextFactory();

            // Act
            var act = () => factory.CreateDbContext([]);

            // Assert
            var exception = Assert.Throws<InvalidOperationException>(act);
            Assert.Contains("DATABASE_URL", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", previous);
        }
    }
}
