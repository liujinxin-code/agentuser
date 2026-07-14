using AgentUserSystem.Infrastructure.Persistence;
using AgentUserSystem.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUserSystem.Infrastructure.DependencyInjection;

/// <summary>
/// Infrastructure 层 IServiceCollection 注册入口。
/// 这里注册 EF Core、Redis、JwtOptions 这类框架服务；
/// 仓储、JWT 服务、Redis token store 等具体实现由 Autofac Module 注册。
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>注册基础设施依赖。</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 手动读取 Jwt 配置，避免额外依赖 Options 绑定扩展包。
        services.Configure<JwtOptions>(options =>
        {
            var section = configuration.GetSection("Jwt");
            options.Issuer = section["Issuer"] ?? options.Issuer;
            options.Audience = section["Audience"] ?? options.Audience;
            options.SecretKey = section["SecretKey"] ?? options.SecretKey;
            options.AccessTokenMinutes = int.TryParse(section["AccessTokenMinutes"], out var accessMinutes)
                ? accessMinutes
                : options.AccessTokenMinutes;
            options.RefreshTokenDays = int.TryParse(section["RefreshTokenDays"], out var refreshDays)
                ? refreshDays
                : options.RefreshTokenDays;
            options.PersistentTokenDays = int.TryParse(section["PersistentTokenDays"], out var persistentDays)
                ? persistentDays
                : options.PersistentTokenDays;
        });

        var connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("缺少 ConnectionStrings:MySql 配置。");

        // 固定 MySQL 版本，避免启动时为了 AutoDetect 主动连接数据库。
        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

        // IDistributedCache 的 Redis 实现，RedisCacheService 会基于它封装泛型读写。
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            options.InstanceName = "AgentUserSystem:";
        });

        return services;
    }
}
