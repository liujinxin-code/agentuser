using AgentUserSystem.Application.Abstractions;
using AgentUserSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentUserSystem.Infrastructure.Persistence;

/// <summary>
/// 用户仓储实现。
/// Application 层只依赖 IUserRepository，不直接依赖 EF Core。
/// </summary>
public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    /// <summary>按用户 ID 查询用户。</summary>
    public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    /// <summary>按用户名查询用户，登录时使用。</summary>
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
    }

    /// <summary>检查用户名是否已存在，注册和创建下级用户时使用。</summary>
    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AnyAsync(x => x.Username == username, cancellationToken);
    }

    /// <summary>检查邮箱是否已存在，注册时使用。</summary>
    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken);
    }

    /// <summary>检查代理名下是否存在下级用户，代理转普通用户时使用。</summary>
    public Task<bool> HasChildrenAsync(int agentUserId, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AnyAsync(x => x.AgentUserId == agentUserId, cancellationToken);
    }

    /// <summary>添加新用户，真正写库由 UnitOfWork.SaveChangesAsync 触发。</summary>
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
    }
}
