using Catalog.Application.Abstractions;
using Catalog.Application.Catalog;
using Catalog.Application.Saga;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        services.AddScoped<ICatalogSagaOrchestrator, CatalogSagaOrchestrator>();
        services.AddScoped<CatalogReadService>();
        return services;
    }
}
