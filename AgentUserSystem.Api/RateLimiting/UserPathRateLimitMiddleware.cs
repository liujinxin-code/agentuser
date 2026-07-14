using System.Security.Claims;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentUserSystem.Api.RateLimiting;

/// <summary>
/// userid:path 维度的 Redis 限流中间件。
/// 它放在 UseAuthentication 之后执行，因此可以通过 HttpContext.User 拿到登录用户 ID。
/// </summary>
public sealed class UserPathRateLimitMiddleware(
    RequestDelegate next,
    IConnectionMultiplexer redis,
    IOptions<UserPathRateLimitOptions> options,
    ILogger<UserPathRateLimitMiddleware> logger)
{
    private readonly UserPathRateLimitOptions _options = options.Value;

    /// <summary>
    /// 处理一次 HTTP 请求。
    /// 如果当前窗口内请求数超过限制，直接返回 429；否则继续进入后续中间件和 Controller。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = NormalizePath(context.Request.Path);
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(userId);

        var limit = isAuthenticated ? _options.AuthenticatedLimit : _options.AnonymousLimit;
        var identityPart = isAuthenticated ? $"user:{userId}" : "anonymous";
        var redisKey = $"{_options.RedisKeyPrefix}:{identityPart}:{path}";

        var database = redis.GetDatabase();

        // StringIncrement 是 Redis 原子操作，并发请求不会把计数加丢。
        var currentCount = await database.StringIncrementAsync(redisKey);

        // 第一次创建 key 时设置过期时间，形成固定 60 秒窗口。
        if (currentCount == 1)
        {
            await database.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(_options.WindowSeconds));
        }

        if (currentCount > limit)
        {
            logger.LogWarning(
                "Rate limit exceeded. Key: {RateLimitKey}, Count: {CurrentCount}, Limit: {Limit}",
                redisKey,
                currentCount,
                limit);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers.RetryAfter = _options.WindowSeconds.ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                message = "请求过于频繁，请稍后再试。",
                limit,
                windowSeconds = _options.WindowSeconds
            });
            return;
        }

        await next(context);
    }

    /// <summary>
    /// 规范化路径，避免 /api/users/me 和 /api/users/me/ 被当成两个限流维度。
    /// 查询字符串不参与限流 key，保证同一个接口维度稳定。
    /// </summary>
    private static string NormalizePath(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrWhiteSpace(value) || value == "/")
        {
            return "root";
        }

        return value.Trim('/').Trim().ToLowerInvariant().Replace('/', ':');
    }
}
