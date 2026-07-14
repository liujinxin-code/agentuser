using AgentUserSystem.Application.Auth;
using AgentUserSystem.Application.Users;
using FluentValidation;

namespace AgentUserSystem.Application.Validation;

/// <summary>登录参数验证器。</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(50);
    }
}

/// <summary>注册参数验证器。</summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(50);
    }
}

/// <summary>刷新 token 参数验证器。</summary>
public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

/// <summary>转代理参数验证器。</summary>
public sealed class BecomeAgentRequestValidator : AbstractValidator<BecomeAgentRequest>
{
    public BecomeAgentRequestValidator()
    {
        RuleFor(x => x.AgentDomain).MaximumLength(255);
    }
}

/// <summary>创建下级用户参数验证器。</summary>
public sealed class CreateSubUserRequestValidator : AbstractValidator<CreateSubUserRequest>
{
    public CreateSubUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(50);
    }
}

/// <summary>代理转赠参数验证器。</summary>
public sealed class TransferToChildRequestValidator : AbstractValidator<TransferToChildRequest>
{
    public TransferToChildRequestValidator()
    {
        RuleFor(x => x.ChildUserId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

/// <summary>代理提现参数验证器。</summary>
public sealed class WithdrawAgentAmountRequestValidator : AbstractValidator<WithdrawAgentAmountRequest>
{
    public WithdrawAgentAmountRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
