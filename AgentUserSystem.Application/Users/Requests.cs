namespace AgentUserSystem.Application.Users;

/// <summary>转代理请求，AgentDomain 可以为空。</summary>
public sealed record BecomeAgentRequest(string? AgentDomain);

/// <summary>代理创建下级用户请求。</summary>
public sealed record CreateSubUserRequest(
    string Email,
    string Username,
    string Password);

/// <summary>代理给下级用户转账请求。</summary>
public sealed record TransferToChildRequest(
    int ChildUserId,
    decimal Amount);

/// <summary>代理提现请求。</summary>
public sealed record WithdrawAgentAmountRequest(decimal Amount);
