using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AgentUserSystem.Application.Abstractions;
using AgentUserSystem.Application.Auth;
using AgentUserSystem.Domain.Entities;
using AutoMapper;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgentUserSystem.Infrastructure.Security;

/// <summary>
/// JWT 生成与解析服务。
/// 这里只负责 token 本身的签名、过期时间、claim 生成和解析；
/// token 是否仍然有效由 RedisTokenStore 或数据库 token_key 决定。
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IMapper mapper) : ITokenService
{
    /// <summary>
    /// IOptions 由 InfrastructureServiceCollectionExtensions.Configure 注入。
    /// 这里缓存 Value，避免每次生成 token 都重复访问 Options 包装对象。
    /// </summary>
    private readonly JwtOptions _options = options.Value;

    /// <summary>
    /// 创建登录态 token 对：
    /// access_token 用于访问 API，refresh_token 用于换取新 access_token。
    /// 返回值里也包含用户持久 token，方便客户端保存。
    /// </summary>
    public TokenResponse CreateTokenPair(User user)
    {
        var accessExpires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var refreshExpires = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);

        var accessToken = CreateJwt(user, accessExpires, "access");
        var refreshToken = CreateJwt(user, refreshExpires, "refresh");

        // 如果用户表中已经有持久 token，就继续返回老 token；
        // 如果还没有，就临时生成一个，调用方会负责写入 user.token_key。
        var persistentToken = string.IsNullOrWhiteSpace(user.TokenKey)
            ? CreatePersistentToken(user)
            : user.TokenKey;

        return new TokenResponse(
            accessToken,
            refreshToken,
            accessExpires,
            refreshExpires,
            persistentToken,
            mapper.Map<UserTokenInfo>(user));
    }

    /// <summary>
    /// 创建用户持久 JWT。
    /// 该 token 的 token_type = persistent，会保存到 tk_user.token_key。
    /// </summary>
    public string CreatePersistentToken(User user)
    {
        return CreateJwt(user, DateTime.UtcNow.AddDays(_options.PersistentTokenDays), "persistent");
    }

    /// <summary>
    /// 为兼容旧接口保留的方法，本质上就是普通 token 哈希。
    /// </summary>
    public string HashRefreshToken(string refreshToken)
    {
        return HashToken(refreshToken);
    }

    /// <summary>
    /// 对 token 做 SHA256。
    /// Redis key 中只保存 token hash，避免把完整 JWT 暴露在 Redis key 和日志里。
    /// </summary>
    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// 读取指定类型 token 中的用户 ID。
    /// expectedTokenType 用于区分 access / refresh / persistent，避免拿错 token 调错接口。
    /// </summary>
    public int? ReadUserIdFromToken(string token, string expectedTokenType, bool validateLifetime)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, CreateValidationParameters(validateLifetime), out var securityToken);
            if (securityToken is not JwtSecurityToken jwt ||
                !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
            {
                return null;
            }

            var tokenType = principal.FindFirst("token_type")?.Value;
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return tokenType == expectedTokenType && int.TryParse(userId, out var id) ? id : null;
        }
        catch
        {
            // token 签名错误、格式错误、过期等都会进这里；
            // 对外统一返回 null，避免泄露校验细节。
            return null;
        }
    }

    /// <summary>
    /// 读取 refresh_token 中的用户 ID。
    /// 这里不校验生命周期，是为了在 refresh 流程里先拿到 userId；
    /// 真正是否允许刷新，还要继续检查 Redis 中 refresh_token 是否存在。
    /// </summary>
    public int? ReadUserIdFromExpiredOrValidToken(string token)
    {
        return ReadUserIdFromToken(token, "refresh", validateLifetime: false);
    }

    /// <summary>
    /// 创建 JWT 校验参数。
    /// API 中间件和手动解析 refresh_token 都共用同一个工厂，避免密钥/签发方配置不一致。
    /// </summary>
    public TokenValidationParameters CreateValidationParameters(bool validateLifetime = true)
    {
        return JwtTokenValidationParametersFactory.Create(_options, validateLifetime);
    }

    /// <summary>
    /// 按指定 token_type 创建 JWT。
    /// token_type 是本项目自定义 claim，用于在中间件和业务代码中区分不同用途的 token。
    /// </summary>
    private string CreateJwt(User user, DateTime expires, string tokenType)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("is_agent", user.Agent ? "true" : "false"),
            new Claim("token_type", tokenType),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
