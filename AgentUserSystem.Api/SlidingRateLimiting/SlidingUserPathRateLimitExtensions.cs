namespace AgentUserSystem.Api.SlidingRateLimiting;

/// <summary>
/// 滑动窗口 userid:path 限流的注册扩展方法。
/// Redis 连接复用固定窗口限流已经注册的 IConnectionMultiplexer。
/// </summary>
public static class SlidingUserPathRateLimitExtensions
{
    /// <summary>注册滑动窗口限流配置。</summary>
    public static IServiceCollection AddSlidingUserPathRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SlidingUserPathRateLimitOptions>(configuration.GetSection("SlidingUserPathRateLimit"));
        return services;
    }

    /// <summary>启用滑动窗口限流中间件。</summary>
    public static IApplicationBuilder UseSlidingUserPathRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SlidingUserPathRateLimitMiddleware>();
    }
}
