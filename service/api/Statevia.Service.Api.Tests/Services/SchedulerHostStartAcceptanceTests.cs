using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Engine;
using Statevia.Service.Api.Hosting;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Services;

/// <summary>Scheduler プロセスの Start 受理（製品 Engine なし）。</summary>
public sealed class SchedulerHostStartAcceptanceTests
{
    /// <summary>Development の Host と同じく、構築時に DI グラフを検証できる。</summary>
    /// <remarks>
    /// <see cref="IHostEnvironment"/> と <see cref="IConfiguration"/> は Generic Host が供給する。このテストは <see cref="ServiceCollection"/> を直接組むため、同じ依存を登録してから検証する。
    /// </remarks>
    [Fact]
    public void AddStateviaSchedulerProcess_ValidateOnBuild_Succeeds()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new SchedulerHostTestEnvironment(Environments.Development));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=statevia;Username=x;Password=y"
        }).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddStateviaSchedulerProcess(configuration);

        // Act
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        // Assert
        Assert.NotNull(provider.GetService<INodesSchemaProvider>());
    }

    /// <summary>製品 Engine を登録しない scheduler プロセスで Start が Start work item を載せる。</summary>
    [Fact]
    public async Task StartAsync_WithoutProductEngine_EnqueuesStartWorkItem()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=statevia;Username=x;Password=y"
        }).Build();
        services.AddStateviaSchedulerProcess(configuration);
        services.AddSingleton(database.Factory);

        Assert.DoesNotContain(services, static descriptor => descriptor.ImplementationType == typeof(ExecutionEngine));
        Assert.Contains(
            services,
            static descriptor => descriptor.ServiceType == typeof(IExecutionEngine)
                && descriptor.ImplementationType == typeof(SchedulerHostRejectedExecutionEngine));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var compiler = scope.ServiceProvider.GetRequiredService<IDefinitionCompilerService>();
        const string yaml = """
            workflow:
              name: W
            states:
              A:
                action: statevia.action.builtin.execution.noop
                on:
                  Completed:
                    end: true
            """;
        var compiled = compiler.ValidateAndCompile("W", yaml);
        var definitionId = Guid.NewGuid();
        await using (var seed = database.Factory.CreateDbContext())
        {
            var project = ProjectTestData.AddDefaultProject(seed, TestTenantIds.DefaultTenantId, "default");
            DefinitionTestData.AddDefinitionWithVersion(
                seed,
                TestTenantIds.DefaultTenantId,
                definitionId,
                "W",
                project.ProjectId,
                sourceYaml: yaml,
                compiledJson: compiled.CompiledJson);
            await seed.SaveChangesAsync();
        }

        var principalId = await SecurityTestSeed.SeedUserAsync(database, "scheduler-start", "password");
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        using var tenantScope = accessor.SetContext(TestTenantIds.DefaultContext with
        {
            PrincipalId = principalId,
            EffectivePermissionKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                WellKnownPermissionKeys.ExecutionsWrite
            }
        });
        var executions = scope.ServiceProvider.GetRequiredService<IExecutionService>();

        // Act
        var started = await executions.StartAsync(
            new StartExecutionRequest { DefinitionId = definitionId.ToString("D") },
            "scheduler-host-start",
            new CommandRequestContext("POST", "/v1/executions"),
            CancellationToken.None);

        // Assert
        Assert.Equal(ExecutionProjectionStatuses.Running, started.Status);
        await using var verify = database.Factory.CreateDbContext();
        var item = Assert.Single(await verify.ExecutionWorkItems.ToListAsync());
        Assert.Equal(ExecutionWorkItemKinds.Start, item.Kind);
        Assert.Equal(started.ResourceId, item.ExecutionId);
    }

    /// <summary>Scheduler ホスト検証用の <see cref="IHostEnvironment"/>。</summary>
    /// <param name="environmentName">ホスト環境名。Development の Generic Host に合わせる。</param>
    private sealed class SchedulerHostTestEnvironment(string environmentName) : IHostEnvironment
    {
        /// <inheritdoc />
        public string EnvironmentName { get; set; } = environmentName;

        /// <inheritdoc />
        public string ApplicationName { get; set; } = "Statevia.Service.Api.Tests";

        /// <inheritdoc />
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        /// <inheritdoc />
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
