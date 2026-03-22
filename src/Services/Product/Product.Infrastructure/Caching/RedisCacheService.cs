using System.Text.Json;
using Microsoft.Extensions.Logging;
using Product.Application.Abstractions;
using StackExchange.Redis;

namespace Product.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        if (!_redis.IsConnected)
        {
            _logger.LogWarning("Redis is unavailable. Cache GET skipped for key {CacheKey}", key);
            return default;
        }

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
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection error on GET for key {CacheKey}. Falling back to database", key);
            return default;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout on GET for key {CacheKey}. Falling back to database", key);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected cache GET error for key {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (!_redis.IsConnected)
        {
            _logger.LogWarning("Redis is unavailable. Cache SET skipped for key {CacheKey}", key);
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            var payload = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, payload, ttl);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection error on SET for key {CacheKey}. Request will continue", key);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout on SET for key {CacheKey}. Request will continue", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected cache SET error for key {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        if (!_redis.IsConnected)
        {
            _logger.LogWarning("Redis is unavailable. Cache DELETE skipped for key {CacheKey}", key);
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection error on DELETE for key {CacheKey}", key);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout on DELETE for key {CacheKey}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected cache DELETE error for key {CacheKey}", key);
        }
    }
}
