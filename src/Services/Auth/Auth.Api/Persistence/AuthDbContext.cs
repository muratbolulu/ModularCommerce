using Auth.Api.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Persistence;

public sealed class AuthDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshTokenEntity>(cfg =>
        {
            cfg.ToTable("RefreshTokens");
            cfg.HasKey(x => x.Id);
            cfg.Property(x => x.Token).IsRequired().HasMaxLength(256);
            cfg.Property(x => x.UserId).IsRequired().HasMaxLength(450);
            cfg.Property(x => x.ExpiresAtUtc).IsRequired();
        });
    }
}
