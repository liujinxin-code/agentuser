namespace AgentUserSystem.Infrastructure.Security;

/// <summary>
/// JWT 配置项，对应 appsettings.json 中的 Jwt 节点。
/// </summary>
public sealed class JwtOptions
{
    /// <summary>JWT 签发方。</summary>
    public string Issuer { get; set; } = "AgentUserSystem";

    /// <summary>JWT 受众。</summary>
    public string Audience { get; set; } = "AgentUserSystem.Client";

    /// <summary>HS256 对称签名密钥，至少 32 字节。</summary>
    public string SecretKey { get; set; } = "CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32_CHARS";

    /// <summary>access_token 有效分钟数。</summary>
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>refresh_token 有效天数。</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>用户持久 token 有效天数。</summary>
    public int PersistentTokenDays { get; set; } = 3650;
}
