using System.Security.Claims;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentUserSystem.Api.SlidingRateLimiting;

/// <summary>
/// userid:path 维度的滑动窗口限流中间件。
/// 和固定窗口不同，滑动窗口永远统计“当前时间往前 WindowSeconds 秒”的请求数。
/// </summary>
public sealed class SlidingUserPathRateLimitMiddleware(
    RequestDelegate next,
    IConnectionMultiplexer redis,
    IOptions<SlidingUserPathRateLimitOptions> options,
    ILogger<SlidingUserPathRateLimitMiddleware> logger)
{
    private const string SlidingWindowScript = """
        redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, ARGV[1])
        local current = redis.call('ZCARD', KEYS[1])
        if current >= tonumber(ARGV[2]) then
            redis.call('EXPIRE', KEYS[1], tonumber(ARGV[3]))
            return {0, current}
        end
        redis.call('ZADD', KEYS[1], ARGV[4], ARGV[5])
        redis.call('EXPIRE', KEYS[1], tonumber(ARGV[3]))
        return {1, current + 1}
        """;

    private readonly SlidingUserPathRateLimitOptions _options = options.Value;

    /// <summary>
    /// 处理一次 HTTP 请求。超限返回 429，否则继续执行后续中间件。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(userId);
        var identityPart = isAuthenticated ? $"user:{userId}" : "anonymous";
        var pathPart = NormalizePath(context.Request.Path);
        var limit = isAuthenticated ? _options.AuthenticatedLimit : _options.AnonymousLimit;
        var redisKey = $"{_options.RedisKeyPrefix}:{identityPart}:{pathPart}";

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoff = now - (_options.WindowSeconds * 1000L);
        var member = $"{now}:{Guid.NewGuid():N}";

        var database = redis.GetDatabase();
        var result = (RedisResult[]?)await database.ScriptEvaluateAsync(
            SlidingWindowScript,
            [redisKey],
            [cutoff, limit, _options.WindowSeconds, now, member]);

        var allowed = result is not null && (int)result[0] == 1;
        var currentCount = result is not null ? (long)result[1] : 0;

        if (!allowed)
        {
            logger.LogWarning(
                "Sliding rate limit exceeded. Key: {RateLimitKey}, Count: {CurrentCount}, Limit: {Limit}",
                redisKey,
                currentCount,
                limit);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers.RetryAfter = _options.WindowSeconds.ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                message = "请求过于频繁，请稍后再试。",
                algorithm = "sliding-window",
                limit,
                windowSeconds = _options.WindowSeconds
            });
            return;
        }

        await next(context);
    }

    /// <summary>
    /// 规范化路径，查询字符串不参与限流 key。
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
