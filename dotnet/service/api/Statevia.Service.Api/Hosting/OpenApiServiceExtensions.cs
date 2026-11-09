using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using Statevia.Service.Api.Hosting.OpenApi;
using System.Reflection;
using System.Text.Json;

namespace Statevia.Service.Api.Hosting;

/// <summary>
/// Swashbuckle（OpenAPI 生成）と Scalar（閲覧 UI）の登録。
/// </summary>
internal static class OpenApiServiceExtensions
{
    private const string DocumentName = "v1";
    private const string ApiDocsEnvironmentVariable = "STATEVIA_ENABLE_API_DOCS";

    /// <summary>
    /// OpenAPI 生成（Swashbuckle）を DI に登録する。
    /// </summary>
    /// <param name="services">サービスコレクション。</param>
    /// <returns>登録後の <paramref name="services"/>。</returns>
    public static IServiceCollection AddStateviaOpenApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "Statevia Service API",
                Version = "v1",
                Description =
                    "Service API HTTP 契約。運用・SSE・冪等・機微 IO 方針等の叙述は docs/specifications/api-http.md を参照。"
            });

            var xmlPath = Path.Combine(
                AppContext.BaseDirectory,
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

            options.MapType<JsonElement>(() => CreateOpenObjectSchema());
            options.MapType<JsonElement?>(() => CreateOpenObjectSchema());

            options.OperationFilter<TenantAndIdempotencyHeadersOperationFilter>();
            options.OperationFilter<DefaultErrorResponsesOperationFilter>();
            options.OperationFilter<ListPagingQueryOperationFilter>();
            options.OperationFilter<ExecutionGraphResponseOperationFilter>();
        });

        return services;
    }

    /// <summary>
    /// Development / Staging / <c>STATEVIA_ENABLE_API_DOCS</c> 時のみ Swagger と Scalar を有効化する。
    /// </summary>
    /// <param name="app">Web アプリケーション。</param>
    /// <returns>同じ <paramref name="app"/>。</returns>
    public static WebApplication UseStateviaOpenApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!IsApiDocsEnabled(app.Environment))
            return app;

        app.UseSwagger();
        app.MapScalarApiReference(
            "/scalar/v1",
            options =>
            {
                options.WithTitle("Statevia Service API");
                options.WithOpenApiRoutePattern($"/swagger/{DocumentName}/swagger.json");
            });

        return app;
    }

    /// <summary>
    /// API ドキュメントエンドポイントを公開してよいか判定する。
    /// </summary>
    /// <param name="environment">ホスト環境。</param>
    /// <returns>Development か Staging、または <c>STATEVIA_ENABLE_API_DOCS=true</c> なら <see langword="true"/>。</returns>
    internal static bool IsApiDocsEnabled(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsDevelopment() || environment.IsStaging())
            return true;

        return string.Equals(
            Environment.GetEnvironmentVariable(ApiDocsEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static OpenApiSchema CreateOpenObjectSchema() =>
        new()
        {
            Type = "object",
            AdditionalPropertiesAllowed = true,
            Description = "任意の JSON オブジェクト"
        };
}
