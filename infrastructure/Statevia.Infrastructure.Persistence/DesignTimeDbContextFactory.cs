using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Statevia.Infrastructure.Persistence;

/// <summary>EF CLI 用 Design-time DbContext ファクトリ。</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException"><c>DATABASE_URL</c> が未設定のとき。</exception>
    public CoreDbContext CreateDbContext(string[] args)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new InvalidOperationException("DATABASE_URL is required for design-time DbContext creation.");
        }

        var connectionString = PostgresConnectionString.Normalize(databaseUrl);

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new CoreDbContext(options, NullTenantContextAccessor.Instance, DisabledTenantQueryFilterOptions.Instance);
    }
}
