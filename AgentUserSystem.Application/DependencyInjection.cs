using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUserSystem.Application;

/// <summary>
/// Application 层 IServiceCollection 注册入口。
/// 注意：业务服务本身通过 Autofac Module 注册，这里只注册框架型服务。
/// </summary>
public static class DependencyInjection
{
    /// <summary>注册 AutoMapper 和 FluentValidation。</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(_ => { }, typeof(DependencyInjection).Assembly);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
