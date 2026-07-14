using AgentUserSystem.Application.Auth;
using AgentUserSystem.Application.Users;
using AgentUserSystem.Domain.Entities;
using AutoMapper;

namespace AgentUserSystem.Application.Mappings;

/// <summary>
/// AutoMapper 映射配置。
/// 负责把领域实体 User 转换成接口返回 DTO 或 token 中的用户简要信息。
/// </summary>
public sealed class UserProfile : Profile
{
    /// <summary>配置 User 到各个输出模型的映射规则。</summary>
    public UserProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.IsAgent, opt => opt.MapFrom(src => src.Agent))
            .ForMember(dest => dest.UserStatus, opt => opt.MapFrom(src => (int)src.UserStatus));

        CreateMap<User, UserTokenInfo>()
            .ForCtorParam(nameof(UserTokenInfo.UserId), opt => opt.MapFrom(src => src.UserId))
            .ForCtorParam(nameof(UserTokenInfo.Username), opt => opt.MapFrom(src => src.Username))
            .ForCtorParam(nameof(UserTokenInfo.IsAgent), opt => opt.MapFrom(src => src.Agent));
    }
}
