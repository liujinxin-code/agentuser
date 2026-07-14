using AgentUserSystem.Application.Abstractions;
using AgentUserSystem.Application.Auth;
using AgentUserSystem.Application.Users;
using Autofac;

namespace AgentUserSystem.Application.AutofacModules;

/// <summary>
/// Application 层 Autofac 模块。
/// 这里集中注册应用服务接口到实现类的映射。
/// </summary>
public sealed class ApplicationAutofacModule : Module
{
    /// <summary>注册应用服务，生命周期为一次请求一个实例。</summary>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AuthService>()
            .As<IAuthService>()
            .InstancePerLifetimeScope();

        builder.RegisterType<UserService>()
            .As<IUserService>()
            .InstancePerLifetimeScope();
    }
}
