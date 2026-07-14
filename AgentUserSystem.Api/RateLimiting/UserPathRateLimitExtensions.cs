using StackExchange.Redis;

namespace AgentUserSystem.Api.RateLimiting;

/// <summary>
/// userid:path 限流的注册扩展方法。
/// </summary>
public static class UserPathRateLimitExtensions
{
    /// <summary>
    /// 注册 Redis 连接和限流配置。
    /// Redis 连接使用单例，避免每次请求重复创建连接。
    /// </summary>
    public static IServiceCollection AddUserPathRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UserPathRateLimitOptions>(configuration.GetSection("UserPathRateLimit"));
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            return ConnectionMultiplexer.Connect(redisConnection);
        });

        return services;
    }

    /// <summary>
    /// 启用 userid:path 限流中间件。
    /// </summary>
    public static IApplicationBuilder UseUserPathRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<UserPathRateLimitMiddleware>();
    }
}
