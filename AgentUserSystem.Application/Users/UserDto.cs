namespace AgentUserSystem.Application.Users;

/// <summary>
/// 用户对外展示 DTO。
/// 不包含 Password 和 TokenKey，避免敏感字段通过普通用户接口泄露。
/// </summary>
public sealed record UserDto(
    int UserId,
    string Email,
    string Username,
    decimal UserAmount,
    bool IsAgent,
    decimal AgentAmount,
    int UserStatus,
    int AgentUserId,
    string? AgentDomain,
    DateTime? CreateTime,
    int? UserVersion);
