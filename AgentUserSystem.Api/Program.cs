using System.Security.Claims;
using AgentUserSystem.Application;
using AgentUserSystem.Application.Abstractions;
using AgentUserSystem.Application.AutofacModules;
using AgentUserSystem.Infrastructure.AutofacModules;
using AgentUserSystem.Infrastructure.DependencyInjection;
using AgentUserSystem.Infrastructure.Security;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    // WebApplicationBuilder 会读取 appsettings.json、环境变量、命令行参数等配置源。
    var builder = WebApplication.CreateBuilder(args);

    // 使用 Autofac 替换 ASP.NET Core 默认 DI 容器。
    // IServiceCollection 仍然用于注册框架组件，业务服务通过 Autofac Module 注册。
    builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
    builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        containerBuilder.RegisterModule<ApplicationAutofacModule>();
        containerBuilder.RegisterModule<InfrastructureAutofacModule>();
    });
    builder.Host.UseSerilog((context, _, loggerConfiguration) =>
    {
        // 控制台输出所有日志，文件只记录 Warning 及以上。
        // WriteTo.Map 会按 SourceContext 拆分文件，SourceContext 通常来自 ILogger<T> 的 T。
        loggerConfiguration
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Map(
                "SourceContext",
                "General",
                (sourceContext, writeTo) => writeTo.Async(asyncWriteTo => asyncWriteTo.File(
                    path: Path.Combine("logs", $"{SanitizeFileName(sourceContext)}-.log"),
                    rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Warning,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")),
                sinkMapCountLimit: 100);
    });

    // 注册 MVC Controller 和请求参数自动验证。
    builder.Services.AddControllers();

    // DataProtection 默认会写用户目录；这里改成写项目内 keys 目录，避免沙箱/权限问题。
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")));
    builder.Services.AddFluentValidationAutoValidation();

    // Swagger 用于开发期调试接口，并配置 Bearer token 输入框。
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Agent User System API", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Input access_token without the Bearer prefix."
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // API 中间件使用同一套 JwtOptions，避免和 JwtTokenService 的密钥/签发方不一致。
    var jwtOptions = ReadJwtOptions(builder.Configuration);

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = JwtTokenValidationParametersFactory.Create(jwtOptions);
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    // JWT 签名和过期时间通过后，还要继续做“业务有效性”判断。
                    // access_token 必须存在 Redis；persistent token 必须等于数据库 token_key。
                    var rawToken = ExtractBearerToken(context.HttpContext.Request);
                    if (string.IsNullOrWhiteSpace(rawToken))
                    {
                        context.Fail("Missing bearer token.");
                        return;
                    }

                    var tokenType = context.Principal?.FindFirst("token_type")?.Value;
                    var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!int.TryParse(userIdValue, out var userId))
                    {
                        context.Fail("Invalid user id.");
                        return;
                    }

                    if (tokenType == "access")
                    {
                        // access_token 支持主动注销：Redis 中不存在就拒绝访问。
                        var tokenStore = context.HttpContext.RequestServices.GetRequiredService<ITokenStore>();
                        if (!await tokenStore.IsAccessTokenActiveAsync(rawToken, context.HttpContext.RequestAborted))
                        {
                            context.Fail("access_token has been revoked or expired.");
                        }

                        return;
                    }

                    if (tokenType == "persistent")
                    {
                        // persistent token 不存 Redis，而是和用户表 token_key 比对。
                        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                        var user = await users.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                        if (user is null || user.TokenKey != rawToken)
                        {
                            context.Fail("persistent token has been revoked or replaced.");
                        }

                        return;
                    }

                    context.Fail("Only access_token or persistent token can call APIs.");
                }
            };
        });

    builder.Services.AddAuthorization();

    // 注册应用层框架服务：AutoMapper、FluentValidation 等。
    builder.Services.AddApplication();

    // 注册基础设施层框架服务：EF Core、Redis、JwtOptions 等。
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    // 记录 HTTP 请求日志。
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // 顺序很重要：先认证，再授权，最后映射控制器。
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapGet("/", () => Results.Redirect("/swagger"));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed.");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// 从配置中读取 JWT 选项。
/// 这里手动读取是为了避免额外依赖 Options 绑定扩展，同时保持和 Infrastructure 层一致。
/// </summary>
static JwtOptions ReadJwtOptions(IConfiguration configuration)
{
    var options = new JwtOptions();
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

    return options;
}

/// <summary>
/// 从 HTTP Authorization header 中提取 Bearer token 原文。
/// Redis 删除 token 时需要原文先计算 hash。
/// </summary>
static string? ExtractBearerToken(HttpRequest request)
{
    var authorization = request.Headers.Authorization.ToString();
    const string prefix = "Bearer ";
    return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? authorization[prefix.Length..].Trim()
        : null;
}

/// <summary>
/// 把 SourceContext 转换成合法文件名。
/// Serilog 按 ILogger<T> 的 T 拆日志文件时会用到。
/// </summary>
static string SanitizeFileName(object? value)
{
    var text = value?.ToString();
    if (string.IsNullOrWhiteSpace(text))
    {
        return "General";
    }

    foreach (var invalidChar in Path.GetInvalidFileNameChars())
    {
        text = text.Replace(invalidChar, '_');
    }

    return text.Replace('"', '_');
}
