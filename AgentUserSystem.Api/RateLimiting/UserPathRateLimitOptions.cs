namespace AgentUserSystem.Api.RateLimiting;

/// <summary>
/// 基于用户和接口路径的限流配置。
/// 已登录用户按 userid:path 限流，匿名请求按 anonymous:path 限流。
/// </summary>
public sealed class UserPathRateLimitOptions
{
    /// <summary>Redis key 前缀，避免和业务缓存 key 冲突。</summary>
    public string RedisKeyPrefix { get; set; } = "rate";

    /// <summary>固定时间窗口，默认 60 秒。</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>已登录用户每个 userid:path 在窗口内允许的请求数。</summary>
    public int AuthenticatedLimit { get; set; } = 200;

    /// <summary>匿名用户每个 anonymous:path 在窗口内允许的请求数。</summary>
    public int AnonymousLimit { get; set; } = 100;
}
