using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Product.Application.Abstractions;
using Product.Infrastructure.Caching;
using Product.Infrastructure.Messaging;
using Product.Infrastructure.Persistence;
using Product.Infrastructure.Repositories;
using StackExchange.Redis;

namespace Product.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ProductDb")
            ?? configuration["PRODUCT_DB_CONNECTION"]
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=ProductDb;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<ProductDbContext>(options => options.UseSqlServer(connectionString));

        var redisConnection = configuration["REDIS_CONNECTION"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnection);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddScoped<IEventPublisher, ConsoleEventPublisher>();

        return services;
    }
}
