namespace AgentUserSystem.Api.SlidingRateLimiting;

/// <summary>
/// 基于 Redis SortedSet 的滑动窗口限流配置。
/// 规则和固定窗口限流一致：登录用户按 userid:path，匿名接口按 anonymous:path。
/// </summary>
public sealed class SlidingUserPathRateLimitOptions
{
    /// <summary>Redis key 前缀。和固定窗口限流使用不同前缀，方便对比两套算法的数据。</summary>
    public string RedisKeyPrefix { get; set; } = "sliding-rate";

    /// <summary>滑动窗口长度，默认 60 秒。</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>已登录用户每个 userid:path 在滑动窗口内允许的请求数。</summary>
    public int AuthenticatedLimit { get; set; } = 200;

    /// <summary>匿名用户每个 anonymous:path 在滑动窗口内允许的请求数。</summary>
    public int AnonymousLimit { get; set; } = 100;
}
