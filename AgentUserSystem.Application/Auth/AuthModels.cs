namespace AgentUserSystem.Application.Auth;

/// <summary>登录请求。</summary>
public sealed record LoginRequest(string Username, string Password);

/// <summary>注册请求。</summary>
public sealed record RegisterRequest(
    string Email,
    string Username,
    string Password);

/// <summary>刷新 token 请求。</summary>
public sealed record RefreshTokenRequest(string RefreshToken);

/// <summary>
/// token 返回模型。
/// PersistentToken 是用户持久 JWT，对应 tk_user.token_key。
/// </summary>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    string PersistentToken,
    UserTokenInfo User);

/// <summary>写入 token 响应中的用户简要信息。</summary>
public sealed record UserTokenInfo(int UserId, string Username, bool IsAgent);
