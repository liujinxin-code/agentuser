using AgentUserSystem.Domain.Enums;

namespace AgentUserSystem.Domain.Entities;

/// <summary>
/// 用户聚合根，对应数据库表 tk_user。
/// 这里放的是用户自身能够保证的业务规则，例如能否转代理、能否给下级转账、代理余额提现等。
/// Application 层只负责组织流程，真正的业务约束尽量收敛在领域对象里。
/// </summary>
public sealed class User
{
    /// <summary>
    /// EF Core 反射创建实体时需要无参构造函数。
    /// private 可以避免业务代码绕过有参构造创建不完整用户。
    /// </summary>
    private User()
    {
    }

    /// <summary>
    /// 创建普通用户。
    /// 如果 agentUserId 大于 0，表示该用户挂在某个代理名下。
    /// </summary>
    public User(string email, string username, string password, string? createBy, int agentUserId = 0)
    {
        Email = email;
        Username = username;
        Password = password;
        UserAmount = 0;
        IsAgent = 0;
        AgentAmount = 0;
        UserStatus = UserStatus.Enabled;
        AgentUserId = agentUserId;
        CreateBy = createBy;
        CreateTime = DateTime.UtcNow;
        UserVersion = 1;
    }

    /// <summary>用户主键，对应 tk_user.userid。</summary>
    public int UserId { get; private set; }

    /// <summary>用户邮箱，注册和联系用。</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>登录用户名。</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>登录密码。当前按原表结构明文保存，生产环境建议改成哈希。</summary>
    public string Password { get; private set; } = string.Empty;

    /// <summary>用户余额，代理给下级转赠时扣减的就是这个余额。</summary>
    public decimal UserAmount { get; private set; }

    /// <summary>是否代理，保持和数据库 int 字段一致：0 普通用户，1 代理。</summary>
    public int IsAgent { get; private set; }

    /// <summary>代理余额，代理提现时从这里转入 UserAmount。</summary>
    public decimal AgentAmount { get; private set; }

    /// <summary>用户状态：0 未启用，1 已启用。</summary>
    public UserStatus UserStatus { get; private set; }

    /// <summary>上级代理用户 ID。0 表示没有上级代理。</summary>
    public int AgentUserId { get; private set; }

    /// <summary>代理域名，只有代理用户才有意义。</summary>
    public string? AgentDomain { get; private set; }

    /// <summary>创建人，一般记录代理创建下级用户时的代理用户名。</summary>
    public string? CreateBy { get; private set; }

    /// <summary>
    /// 用户持久 JWT。
    /// 注意：这里不是 access_token，也不是 refresh_token；它用于后续长期直接调用 API。
    /// </summary>
    public string? TokenKey { get; private set; }

    /// <summary>创建时间。</summary>
    public DateTime? CreateTime { get; private set; }

    /// <summary>用户信息版本号，每次关键字段变化时递增。</summary>
    public int? UserVersion { get; private set; }

    /// <summary>判断用户是否启用，方便业务代码表达意图。</summary>
    public bool IsEnabled => UserStatus == UserStatus.Enabled;

    /// <summary>判断用户是否代理，屏蔽数据库 int 字段细节。</summary>
    public bool Agent => IsAgent == 1;

    /// <summary>
    /// 确保用户处于启用状态。
    /// 领域行为入口统一先调用它，避免禁用用户继续发生业务动作。
    /// </summary>
    public void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("用户未启用。");
        }
    }

    /// <summary>
    /// 普通用户转为代理。
    /// 业务规则：只有没有上级代理的普通用户才能转代理。
    /// </summary>
    public void BecomeAgent(string? domain)
    {
        EnsureEnabled();
        if (Agent)
        {
            return;
        }

        if (AgentUserId != 0)
        {
            throw new InvalidOperationException("存在上级代理的普通用户不能转为代理。");
        }

        IsAgent = 1;
        AgentDomain = domain;
        Touch();
    }

    /// <summary>
    /// 代理转为普通用户。
    /// 业务规则：代理名下没有下级用户时才允许转为普通用户。
    /// </summary>
    public void BecomeNormal(bool hasChildren)
    {
        EnsureEnabled();
        if (!Agent)
        {
            return;
        }

        if (hasChildren)
        {
            throw new InvalidOperationException("代理存在下级用户，不能转为普通用户。");
        }

        IsAgent = 0;
        AgentDomain = null;
        Touch();
    }

    /// <summary>
    /// 代理给自己的下级用户转赠用户余额。
    /// 注意：扣减的是代理自己的 UserAmount，不是 AgentAmount。
    /// </summary>
    public void TransferUserAmountToChild(User child, decimal amount)
    {
        EnsureEnabled();
        child.EnsureEnabled();

        if (!Agent)
        {
            throw new InvalidOperationException("只有代理可以给下级用户转赠余额。");
        }

        if (child.AgentUserId != UserId)
        {
            throw new InvalidOperationException("只能给自己的下级用户转赠余额。");
        }

        if (amount <= 0)
        {
            throw new InvalidOperationException("转赠金额必须大于 0。");
        }

        if (UserAmount < amount)
        {
            throw new InvalidOperationException("用户余额不足。");
        }

        UserAmount -= amount;
        child.UserAmount += amount;
        Touch();
        child.Touch();
    }

    /// <summary>
    /// 代理提现：把 AgentAmount 转入 UserAmount。
    /// 这里不涉及外部支付，只是两个账户字段之间的余额转移。
    /// </summary>
    public void WithdrawAgentAmount(decimal amount)
    {
        EnsureEnabled();
        if (!Agent)
        {
            throw new InvalidOperationException("只有代理可以提现代理余额。");
        }

        if (amount <= 0)
        {
            throw new InvalidOperationException("提现金额必须大于 0。");
        }

        if (AgentAmount < amount)
        {
            throw new InvalidOperationException("代理余额不足。");
        }

        AgentAmount -= amount;
        UserAmount += amount;
        Touch();
    }

    /// <summary>
    /// 写入或替换用户持久 JWT。
    /// 该 token 会存入 tk_user.token_key，用于后续长期调用 API。
    /// </summary>
    public void SetPersistentTokenKey(string persistentToken)
    {
        TokenKey = persistentToken;
        Touch();
    }

    /// <summary>
    /// 每次修改用户核心信息时递增版本号。
    /// 后续如果需要乐观锁或缓存版本校验，可以复用该字段。
    /// </summary>
    private void Touch()
    {
        UserVersion = (UserVersion ?? 0) + 1;
    }
}
