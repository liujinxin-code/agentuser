using AgentUserSystem.Application.Abstractions;
using AgentUserSystem.Application.Auth;
using AgentUserSystem.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUserSystem.Api.Controllers;

/// <summary>
/// 认证接口。
/// 包含注册、登录、刷新 token 和注销当前 access_token。
/// </summary>
public sealed class AuthController(IAuthService authService) : ApiControllerBase
{
    /// <summary>
    /// 注册用户。
    /// 注册成功后会返回 access_token、refresh_token 和 persistent token。
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// 登录。
    /// 登录成功后 access_token / refresh_token 会写入 Redis。
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<Result<TokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        // return result.Succeeded ? Ok(result.Value) : Unauthorized(new { message = result.Error });
        return result;
    }

    /// <summary>
    /// 使用 refresh_token 换取新的 token 对。
    /// refresh_token 必须仍存在 Redis 中。
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : Unauthorized(new { message = result.Error });
    }

    /// <summary>
    /// 注销当前 access_token。
    /// 这里只注销本次请求携带的 access_token，不清除用户表中的 persistent token。
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(CurrentUserId, CurrentBearerToken(), cancellationToken);
        return result.Succeeded ? NoContent() : FromError(result.Error);
    }

    /// <summary>
    /// 从 Authorization 请求头中提取原始 Bearer token。
    /// 注销时需要原始 token，才能删除 Redis 中对应的 token hash。
    /// </summary>
    private string? CurrentBearerToken()
    {
        var authorization = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        //测试master提交
        //测试分支提交 123
        //[xx.length..]等同于substring(xx.leng) 从xx的最后一位截取到字符串末尾
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
    }
}
