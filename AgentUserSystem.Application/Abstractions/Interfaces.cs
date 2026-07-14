using AgentUserSystem.Application.Auth;
using AgentUserSystem.Application.Common;
using AgentUserSystem.Application.Users;
using AgentUserSystem.Domain.Entities;

namespace AgentUserSystem.Application.Abstractions;

/// <summary>
/// 用户仓储接口。
/// Application 层通过接口访问用户数据，具体 EF Core 实现在 Infrastructure 层。
/// </summary>
public interface IUserRepository
{
    /// <summary>根据用户 ID 查询用户。</summary>
    Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>根据用户名查询用户，登录流程使用。</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>判断用户名是否已经存在。</summary>
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>判断邮箱是否已经存在。</summary>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>判断某个代理是否已经有下级用户。</summary>
    Task<bool> HasChildrenAsync(int agentUserId, CancellationToken cancellationToken = default);

    /// <summary>添加新用户，提交事务由 IUnitOfWork 完成。</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

/// <summary>
/// 工作单元接口。
/// 用于统一提交一次业务用例中的数据库变更。
/// </summary>
public interface IUnitOfWork
{
    /// <summary>提交数据库变更。</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// JWT 服务接口。
/// 负责生成 token、解析 token 和对 token 做 hash。
/// </summary>
public interface ITokenService
{
    /// <summary>创建 access_token 和 refresh_token。</summary>
    TokenResponse CreateTokenPair(User user);

    /// <summary>创建用户持久 JWT，保存到 tk_user.token_key。</summary>
    string CreatePersistentToken(User user);

    /// <summary>refresh_token 哈希，保留该方法是为了语义清晰。</summary>
    string HashRefreshToken(string refreshToken);

    /// <summary>任意 token 哈希，Redis key 使用 hash 而不是原始 token。</summary>
    string HashToken(string token);

    /// <summary>从指定类型 token 中读取用户 ID。</summary>
    int? ReadUserIdFromToken(string token, string expectedTokenType, bool validateLifetime);

    /// <summary>从 refresh_token 中读取用户 ID，允许 token 已过期。</summary>
    int? ReadUserIdFromExpiredOrValidToken(string token);
}

/// <summary>
/// Redis 缓存抽象。
/// Application 层不直接引用 StackExchange.Redis 或 IDistributedCache。
/// </summary>
public interface ICacheService
{
    /// <summary>读取缓存并反序列化为指定类型。</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>写入缓存并设置绝对过期时间。</summary>
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default);

    /// <summary>删除指定缓存 key。</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// token 存储抽象。
/// 当前实现使用 Redis 保存 access_token / refresh_token 的有效状态。
/// </summary>
public interface ITokenStore
{
    /// <summary>保存一组 access_token / refresh_token。</summary>
    Task StoreTokenPairAsync(TokenResponse token, CancellationToken cancellationToken = default);

    /// <summary>判断 access_token 是否仍然有效。</summary>
    Task<bool> IsAccessTokenActiveAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>判断 refresh_token 是否仍然有效。</summary>
    Task<bool> IsRefreshTokenActiveAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>注销 access_token。</summary>
    Task RevokeAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>注销 refresh_token。</summary>
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// 认证应用服务接口。
/// 控制器只依赖该接口，不直接碰仓储、JWT 或 Redis。
/// </summary>
public interface IAuthService
{
    /// <summary>注册用户并返回 token。</summary>
    Task<Result<TokenResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>登录并返回 token。</summary>
    Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>刷新 token。</summary>
    Task<Result<TokenResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>注销当前 access_token。</summary>
    Task<Result> LogoutAsync(int userId, string? accessToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// 用户业务应用服务接口。
/// 包含代理转换、创建下级、转赠余额、提现等业务用例。
/// </summary>
public interface IUserService
{
    /// <summary>获取当前用户信息。</summary>
    Task<Result<UserDto>> GetCurrentAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>普通用户转代理。</summary>
    Task<Result<UserDto>> BecomeAgentAsync(int userId, BecomeAgentRequest request, CancellationToken cancellationToken = default);

    /// <summary>代理转普通用户。</summary>
    Task<Result<UserDto>> BecomeNormalAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>代理创建下级用户。</summary>
    Task<Result<UserDto>> CreateSubUserAsync(int agentUserId, CreateSubUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>代理给下级用户转赠余额。</summary>
    Task<Result> TransferToChildAsync(int agentUserId, TransferToChildRequest request, CancellationToken cancellationToken = default);

    /// <summary>代理提现。</summary>
    Task<Result<UserDto>> WithdrawAgentAmountAsync(int agentUserId, WithdrawAgentAmountRequest request, CancellationToken cancellationToken = default);
}
