using System.Text.Json;
using AgentUserSystem.Application.Abstractions;
using Microsoft.Extensions.Caching.Distributed;

namespace AgentUserSystem.Infrastructure.Caching;

/// <summary>
/// Redis 缓存服务实现。
/// 对 Application 层隐藏 IDistributedCache 和 JSON 序列化细节。
/// </summary>
public sealed class RedisCacheService(IDistributedCache cache) : ICacheService
{
    /// <summary>统一 JSON 序列化配置，使用 Web 默认命名规则。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>读取缓存字符串并反序列化为指定类型。</summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var json = await cache.GetStringAsync(key, cancellationToken);
        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    /// <summary>把对象序列化成 JSON 后写入 Redis，并设置绝对过期时间。</summary>
    public Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return cache.SetStringAsync(
            key,
            json,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration },
            cancellationToken);
    }

    /// <summary>删除指定缓存 key。</summary>
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return cache.RemoveAsync(key, cancellationToken);
    }
}
