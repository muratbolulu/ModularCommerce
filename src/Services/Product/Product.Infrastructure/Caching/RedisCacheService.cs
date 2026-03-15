using System.Text.Json;
using Product.Application.Abstractions;
using StackExchange.Redis;

namespace Product.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (value.IsNullOrEmpty)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try
        {
            var db = _redis.GetDatabase();
            var payload = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, payload, ttl);
        }
        catch
        {
            // Fallback to database if Redis is temporarily unavailable.
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch
        {
            // Cache invalidation can be retried in the next request cycle.
        }
    }
}
