using AgentUserSystem.Application.Abstractions;
using AgentUserSystem.Application.Auth;

namespace AgentUserSystem.Infrastructure.Security;

/// <summary>
/// Redis token 存储。
/// access_token 和 refresh_token 都存 Redis，用来支持主动注销和 refresh_token 轮换。
/// 持久 token 不存这里，它存放在 tk_user.token_key。
/// </summary>
public sealed class RedisTokenStore(ICacheService cache, ITokenService tokens) : ITokenStore
{
    /// <summary>access_token 的 Redis key。key 中使用 hash，不直接暴露原始 token。</summary>
    private static string AccessKey(string tokenHash) => $"auth:access:{tokenHash}";

    /// <summary>refresh_token 的 Redis key。key 中使用 hash，不直接暴露原始 token。</summary>
    private static string RefreshKey(string tokenHash) => $"auth:refresh:{tokenHash}";

    /// <summary>
    /// 登录或刷新成功后保存 access_token 和 refresh_token。
    /// Redis 过期时间与 JWT 过期时间保持一致，这样 token 到期后 Redis 会自动清理。
    /// </summary>
    public async Task StoreTokenPairAsync(TokenResponse token, CancellationToken cancellationToken = default)
    {
        var accessTtl = token.AccessTokenExpiresAt - DateTime.UtcNow;
        var refreshTtl = token.RefreshTokenExpiresAt - DateTime.UtcNow;

        if (accessTtl > TimeSpan.Zero)
        {
            await cache.SetAsync(
                AccessKey(tokens.HashToken(token.AccessToken)),
                token.User.UserId,
                accessTtl,
                cancellationToken);
        }

        if (refreshTtl > TimeSpan.Zero)
        {
            await cache.SetAsync(
                RefreshKey(tokens.HashToken(token.RefreshToken)),
                token.User.UserId,
                refreshTtl,
                cancellationToken);
        }
    }

    /// <summary>
    /// 判断 access_token 是否仍处于有效登录态。
    /// JWT 签名通过但 Redis 不存在，也视为已注销或已失效。
    /// </summary>
    public async Task<bool> IsAccessTokenActiveAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var userId = await cache.GetAsync<int?>(AccessKey(tokens.HashToken(accessToken)), cancellationToken);
        return userId is > 0;
    }

    /// <summary>
    /// 判断 refresh_token 是否还能用于刷新。
    /// refresh_token 被使用一次后会删除，避免反复复用。
    /// </summary>
    public async Task<bool> IsRefreshTokenActiveAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var userId = await cache.GetAsync<int?>(RefreshKey(tokens.HashToken(refreshToken)), cancellationToken);
        return userId is > 0;
    }

    /// <summary>主动注销 access_token，让当前登录态立即不可再访问 API。</summary>
    public Task RevokeAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        return cache.RemoveAsync(AccessKey(tokens.HashToken(accessToken)), cancellationToken);
    }

    /// <summary>删除 refresh_token，通常用于刷新令牌轮换。</summary>
    public Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return cache.RemoveAsync(RefreshKey(tokens.HashToken(refreshToken)), cancellationToken);
    }
}
