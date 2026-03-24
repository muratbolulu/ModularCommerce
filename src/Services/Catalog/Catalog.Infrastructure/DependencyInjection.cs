using Catalog.Application.Abstractions;
using Catalog.Infrastructure.Messaging;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CatalogDb")
            ?? configuration["CATALOG_DB_CONNECTION"]
            ?? "Server=DESKTOP-7CMCCUI\\SQLEXPRESS2022;Database=ModulerCommerceCatalogDb;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICatalogSagaRepository, CatalogSagaRepository>();
        services.AddHostedService<CatalogRabbitMqConsumer>();

        return services;
    }
}
