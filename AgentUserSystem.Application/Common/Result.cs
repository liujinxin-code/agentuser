namespace AgentUserSystem.Application.Common;

/// <summary>
/// 无返回值业务结果。
/// 用它统一表达成功/失败，避免应用服务直接返回异常给控制器。
/// </summary>
public sealed record Result(bool Succeeded, string? Error)
{
    public static Result Success() => new(true, null);
    public static Result Fail(string error) => new(false, error);
}

/// <summary>
/// 带返回值业务结果。
/// 成功时 Value 有值，失败时 Error 有值。
/// </summary>
public sealed record Result<T>(bool Succeeded, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
