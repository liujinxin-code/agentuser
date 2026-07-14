using AgentUserSystem.Application.Abstractions;
using AgentUserSystem.Infrastructure.Caching;
using AgentUserSystem.Infrastructure.Persistence;
using AgentUserSystem.Infrastructure.Security;
using Autofac;

namespace AgentUserSystem.Infrastructure.AutofacModules;

/// <summary>
/// Infrastructure 层 Autofac 模块。
/// 这里集中注册仓储、工作单元、JWT 服务、Redis token store 等基础设施实现。
/// </summary>
public sealed class InfrastructureAutofacModule : Module
{
    /// <summary>注册基础设施服务，生命周期为一次请求一个实例。</summary>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<UserRepository>()
            .As<IUserRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<UnitOfWork>()
            .As<IUnitOfWork>()
            .InstancePerLifetimeScope();

        builder.RegisterType<JwtTokenService>()
            .As<ITokenService>()
            .InstancePerLifetimeScope();

        builder.RegisterType<RedisTokenStore>()
            .As<ITokenStore>()
            .InstancePerLifetimeScope();

        builder.RegisterType<RedisCacheService>()
            .As<ICacheService>()
            .InstancePerLifetimeScope();
    }
}
