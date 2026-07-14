using AgentUserSystem.Application.Abstractions;
using AgentUserSystem.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentUserSystem.Api.Controllers;

/// <summary>
/// 用户业务接口。
/// 所有接口都要求认证，可以使用 access_token，也可以使用 user.token_key 中的 persistent token。
/// </summary>
[Authorize]
public sealed class UsersController(IUserService userService) : ApiControllerBase
{
    /// <summary>获取当前用户信息。</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await userService.GetCurrentAsync(CurrentUserId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : FromError(result.Error);
    }

    /// <summary>当前普通用户转为代理。</summary>
    [HttpPost("me/become-agent")]
    public async Task<IActionResult> BecomeAgent(BecomeAgentRequest request, CancellationToken cancellationToken)
    {
        var result = await userService.BecomeAgentAsync(CurrentUserId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : FromError(result.Error);
    }

    /// <summary>当前代理转为普通用户。</summary>
    [HttpPost("me/become-normal")]
    public async Task<IActionResult> BecomeNormal(CancellationToken cancellationToken)
    {
        var result = await userService.BecomeNormalAsync(CurrentUserId, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : FromError(result.Error);
    }

    /// <summary>代理创建直属下级用户。</summary>
    [HttpPost("sub-users")]
    public async Task<IActionResult> CreateSubUser(CreateSubUserRequest request, CancellationToken cancellationToken)
    {
        var result = await userService.CreateSubUserAsync(CurrentUserId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : FromError(result.Error);
    }

    /// <summary>代理给直属下级用户转赠 user_amount。</summary>
    [HttpPost("sub-users/transfer")]
    public async Task<IActionResult> TransferToChild(TransferToChildRequest request, CancellationToken cancellationToken)
    {
        var result = await userService.TransferToChildAsync(CurrentUserId, request, cancellationToken);
        return result.Succeeded ? NoContent() : FromError(result.Error);
    }

    /// <summary>代理把 agent_amount 提现到 user_amount。</summary>
    [HttpPost("me/withdraw-agent-amount")]
    public async Task<IActionResult> WithdrawAgentAmount(WithdrawAgentAmountRequest request, CancellationToken cancellationToken)
    {
        var result = await userService.WithdrawAgentAmountAsync(CurrentUserId, request, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : FromError(result.Error);
    }
}
