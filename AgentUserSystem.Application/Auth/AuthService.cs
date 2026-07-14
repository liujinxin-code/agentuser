using AgentUserSystem.Application.Abstractions;
using AgentUserSystem.Application.Common;
using AgentUserSystem.Domain.Entities;

namespace AgentUserSystem.Application.Auth;

/// <summary>
/// 认证应用服务。
/// 负责注册、登录、刷新令牌、注销等用例编排：
/// 1. 用户数据由仓储读取和保存；
/// 2. JWT 由 ITokenService 生成和解析；
/// 3. access_token / refresh_token 的有效性由 Redis 中的 ITokenStore 控制；
/// 4. 用户持久 JWT 存在 tk_user.token_key 中。
/// </summary>
public sealed class AuthService(
    IUserRepository users,
    ITokenService tokens,
    ITokenStore tokenStore,
    IUnitOfWork unitOfWork) : IAuthService
{
    /// <summary>
    /// 注册普通用户。
    /// 注册成功后会立即生成：
    /// access_token、refresh_token、persistent token。
    /// persistent token 会写入用户表 token_key，access/refresh 会写入 Redis。
    /// </summary>
    public async Task<Result<TokenResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (await users.ExistsByUsernameAsync(request.Username, cancellationToken))
        {
            return Result<TokenResponse>.Fail("用户名已存在。");
        }

        if (await users.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            return Result<TokenResponse>.Fail("邮箱已存在。");
        }

        // 第一次保存是为了让数据库生成自增 userid。
        // persistent token 的 claim 里需要 userid，所以必须先拿到主键。
        var user = new User(request.Email, request.Username, request.Password, request.Username);
        await users.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 持久 token 保存在 tk_user.token_key，不放 Redis。
        user.SetPersistentTokenKey(tokens.CreatePersistentToken(user));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await IssueTokenAsync(user, cancellationToken);
    }

    /// <summary>
    /// 登录。
    /// 校验账号密码后颁发新的 access_token / refresh_token，并写入 Redis。
    /// 如果老用户还没有 token_key，会补发一个持久 token。
    /// </summary>
    public async Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByUsernameAsync(request.Username, cancellationToken);
        if (user is null || user.Password != request.Password)
        {
            return Result<TokenResponse>.Fail("用户名或密码错误。");
        }

        if (!user.IsEnabled)
        {
            return Result<TokenResponse>.Fail("用户未启用。");
        }

        if (string.IsNullOrWhiteSpace(user.TokenKey))
        {
            user.SetPersistentTokenKey(tokens.CreatePersistentToken(user));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await IssueTokenAsync(user, cancellationToken);
    }

    /// <summary>
    /// 刷新 access_token。
    /// 先校验 refresh_token 的 JWT 签名和用户 ID，再检查 Redis 中是否仍存在该 refresh_token。
    /// Redis 不存在就说明它过期、被注销、或已经被刷新轮换掉。
    /// </summary>
    public async Task<Result<TokenResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var userId = tokens.ReadUserIdFromExpiredOrValidToken(request.RefreshToken);
        if (userId is null)
        {
            return Result<TokenResponse>.Fail("refresh_token 无效。");
        }

        if (!await tokenStore.IsRefreshTokenActiveAsync(request.RefreshToken, cancellationToken))
        {
            return Result<TokenResponse>.Fail("refresh_token 已过期或已失效。");
        }

        var user = await users.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null || !user.IsEnabled)
        {
            return Result<TokenResponse>.Fail("用户不存在或未启用。");
        }

        // refresh_token 使用一次后立即删除，随后颁发一组新的 token。
        // 这能降低 refresh_token 泄露后的复用风险。
        await tokenStore.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        return await IssueTokenAsync(user, cancellationToken);
    }

    /// <summary>
    /// 注销当前 access_token。
    /// 持久 token 不在这里注销；如果后续需要注销持久 token，可以单独增加接口清空 user.token_key。
    /// </summary>
    public async Task<Result> LogoutAsync(int userId, string? accessToken, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Fail("用户不存在。");
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            await tokenStore.RevokeAccessTokenAsync(accessToken, cancellationToken);
        }

        return Result.Success();
    }

    /// <summary>
    /// 统一颁发 access_token / refresh_token。
    /// TokenResponse 中也会带出 persistent token，方便客户端保存。
    /// </summary>
    private async Task<Result<TokenResponse>> IssueTokenAsync(User user, CancellationToken cancellationToken)
    {
        var tokenPair = tokens.CreateTokenPair(user);
        await tokenStore.StoreTokenPairAsync(tokenPair, cancellationToken);
        return Result<TokenResponse>.Success(tokenPair);
    }
}
