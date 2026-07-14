using AgentUserSystem.Domain.Entities;
using AgentUserSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AgentUserSystem.Infrastructure.Persistence;

/// <summary>
/// EF Core 数据库上下文。
/// 目前只映射 tk_user 表，后续新增表时继续在这里添加 DbSet 和 Fluent API 配置。
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>用户表集合，对应数据库 tk_user。</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// 使用 Fluent API 显式映射数据库字段。
    /// 这样领域实体可以使用 C# 风格命名，不必被数据库下划线字段名污染。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            // 表名和主键映射。
            entity.ToTable("tk_user");
            entity.HasKey(x => x.UserId);

            // 字段映射基本按用户提供的建表语句保持一致。
            entity.Property(x => x.UserId).HasColumnName("userid");
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
            entity.Property(x => x.Password).HasColumnName("password").HasMaxLength(50).IsRequired();
            entity.Property(x => x.UserAmount).HasColumnName("user_amount").HasPrecision(10, 2);
            entity.Property(x => x.IsAgent).HasColumnName("is_agent");
            entity.Property(x => x.AgentAmount).HasColumnName("agent_amount").HasPrecision(10, 2);
            entity.Property(x => x.UserStatus)
                .HasColumnName("user_status")
                .HasConversion<int>();
            entity.Property(x => x.AgentUserId).HasColumnName("agent_userid").HasDefaultValue(0);
            entity.Property(x => x.AgentDomain).HasColumnName("agent_domain").HasMaxLength(255);
            entity.Property(x => x.CreateBy).HasColumnName("createby").HasMaxLength(255);
            // token_key 存用户持久 JWT，255 不够，数据库也需要同步改成 varchar(2048) 或 text。
            entity.Property(x => x.TokenKey).HasColumnName("token_key").HasMaxLength(2048);
            entity.Property(x => x.CreateTime).HasColumnName("create_time");
            entity.Property(x => x.UserVersion).HasColumnName("user_version");

            // 用户名查询频繁，保持原表 idx_username 索引。
            entity.HasIndex(x => x.Username).HasDatabaseName("idx_username");
        });
    }
}
