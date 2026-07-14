using AgentUserSystem.Application.Abstractions;
using AgentUserSystem.Application.Common;
using AgentUserSystem.Domain.Entities;
using AutoMapper;

namespace AgentUserSystem.Application.Users;

/// <summary>
/// 用户业务应用服务。
/// 它负责把“取数据、调用领域行为、保存、清缓存、返回 DTO”串起来。
/// 真正的业务限制，例如代理能否转普通用户，放在 User 领域实体中。
/// </summary>
public sealed class UserService(
    IUserRepository users,
    ICacheService cache,
    IMapper mapper,
    IUnitOfWork unitOfWork) : IUserService
{
    /// <summary>用户详情缓存 key。只缓存读取频繁、变化后能明确删除的数据。</summary>
    private static string UserCacheKey(int userId) => $"users:{userId}";

    /// <summary>
    /// 获取当前登录用户信息。
    /// 先读 Redis 缓存，未命中再读数据库并回写缓存。
    /// </summary>
    public async Task<Result<UserDto>> GetCurrentAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = UserCacheKey(userId);
        var cached = await cache.GetAsync<UserDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return Result<UserDto>.Success(cached);
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result<UserDto>.Fail("用户不存在。");
        }

        var dto = mapper.Map<UserDto>(user);
        await cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);
        return Result<UserDto>.Success(dto);
    }

    /// <summary>
    /// 当前用户转为代理。
    /// 领域实体会校验“没有上级代理”这个规则。
    /// </summary>
    public async Task<Result<UserDto>> BecomeAgentAsync(int userId, BecomeAgentRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result<UserDto>.Fail("用户不存在。");
        }

        try
        {
            user.BecomeAgent(request.AgentDomain);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await cache.RemoveAsync(UserCacheKey(user.UserId), cancellationToken);
            return Result<UserDto>.Success(mapper.Map<UserDto>(user));
        }
        catch (InvalidOperationException ex)
        {
            return Result<UserDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 当前代理转为普通用户。
    /// 转换前先查询是否存在下级用户，领域实体根据 hasChildren 决定是否允许转换。
    /// </summary>
    public async Task<Result<UserDto>> BecomeNormalAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result<UserDto>.Fail("用户不存在。");
        }

        var hasChildren = await users.HasChildrenAsync(userId, cancellationToken);
        try
        {
            user.BecomeNormal(hasChildren);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await cache.RemoveAsync(UserCacheKey(user.UserId), cancellationToken);
            return Result<UserDto>.Success(mapper.Map<UserDto>(user));
        }
        catch (InvalidOperationException ex)
        {
            return Result<UserDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 代理创建下级用户。
    /// 创建出的用户自动设置 AgentUserId = 当前代理用户 ID。
    /// </summary>
    public async Task<Result<UserDto>> CreateSubUserAsync(int agentUserId, CreateSubUserRequest request, CancellationToken cancellationToken = default)
    {
        var agent = await users.GetByIdAsync(agentUserId, cancellationToken);
        if (agent is null)
        {
            return Result<UserDto>.Fail("代理用户不存在。");
        }

        if (!agent.Agent)
        {
            return Result<UserDto>.Fail("只有代理可以创建下级用户。");
        }

        if (await users.ExistsByUsernameAsync(request.Username, cancellationToken))
        {
            return Result<UserDto>.Fail("用户名已存在。");
        }

        var child = new User(request.Email, request.Username, request.Password, agent.Username, agent.UserId);
        await users.AddAsync(child, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<UserDto>.Success(mapper.Map<UserDto>(child));
    }

    /// <summary>
    /// 代理把自己的用户余额转赠给直属下级用户。
    /// 成功后清理代理和下级用户的缓存，避免余额显示旧值。
    /// </summary>
    public async Task<Result> TransferToChildAsync(int agentUserId, TransferToChildRequest request, CancellationToken cancellationToken = default)
    {
        var agent = await users.GetByIdAsync(agentUserId, cancellationToken);
        var child = await users.GetByIdAsync(request.ChildUserId, cancellationToken);
        if (agent is null || child is null)
        {
            return Result.Fail("代理或下级用户不存在。");
        }

        try
        {
            agent.TransferUserAmountToChild(child, request.Amount);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await cache.RemoveAsync(UserCacheKey(agent.UserId), cancellationToken);
            await cache.RemoveAsync(UserCacheKey(child.UserId), cancellationToken);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 代理提现：把 agent_amount 转入 user_amount。
    /// 这里只做账户字段转换，不调用外部支付渠道。
    /// </summary>
    public async Task<Result<UserDto>> WithdrawAgentAmountAsync(int agentUserId, WithdrawAgentAmountRequest request, CancellationToken cancellationToken = default)
    {
        var agent = await users.GetByIdAsync(agentUserId, cancellationToken);
        if (agent is null)
        {
            return Result<UserDto>.Fail("代理用户不存在。");
        }

        try
        {
            agent.WithdrawAgentAmount(request.Amount);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await cache.RemoveAsync(UserCacheKey(agent.UserId), cancellationToken);
            return Result<UserDto>.Success(mapper.Map<UserDto>(agent));
        }
        catch (InvalidOperationException ex)
        {
            return Result<UserDto>.Fail(ex.Message);
        }
    }
}
