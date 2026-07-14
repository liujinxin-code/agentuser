using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AgentUserSystem.Infrastructure.Security;

/// <summary>
/// JWT 校验参数工厂。
/// API 中间件和 JwtTokenService 都通过它创建 TokenValidationParameters，避免配置分叉。
/// </summary>
public static class JwtTokenValidationParametersFactory
{
    /// <summary>
    /// 创建 JWT 校验参数。
    /// validateLifetime=false 时通常用于读取已过期 token 中的用户 ID。
    /// </summary>
    public static TokenValidationParameters Create(JwtOptions options, bool validateLifetime = true)
    {
        if (string.IsNullOrWhiteSpace(options.SecretKey) || Encoding.UTF8.GetByteCount(options.SecretKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 bytes.");
        }

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = validateLifetime,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }
}
