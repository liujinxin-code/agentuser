namespace AgentUserSystem.Domain.Enums;

/// <summary>用户状态枚举，对应 tk_user.user_status。</summary>
public enum UserStatus
{
    /// <summary>未启用。</summary>
    Disabled = 0,

    /// <summary>已启用。</summary>
    Enabled = 1
}
