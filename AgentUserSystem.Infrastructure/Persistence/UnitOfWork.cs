using AgentUserSystem.Application.Abstractions;

namespace AgentUserSystem.Infrastructure.Persistence;

/// <summary>
/// 工作单元实现。
/// 当前项目只有一个 DbContext，所以工作单元就是对 SaveChangesAsync 的封装。
/// </summary>
public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    /// <summary>提交当前 DbContext 中跟踪到的所有变更。</summary>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
