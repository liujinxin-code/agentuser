using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace AgentUserSystem.Api.Controllers;

/// <summary>
/// API 控制器基类。
/// 放置所有控制器通用的路由约定、当前用户 ID 获取和错误返回格式。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// 从 JWT 的 NameIdentifier claim 中读取当前用户 ID。
    /// 该值由 JwtTokenService 创建 token 时写入。
    /// </summary>
    protected int CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var userId) ? userId : 0;
        }
    }

    /// <summary>
    /// 统一业务错误响应，避免每个接口重复 new BadRequest。
    /// </summary>
    protected IActionResult FromError(string? error)
    {
        return BadRequest(new { message = error ?? "请求失败。" });
    }
}
