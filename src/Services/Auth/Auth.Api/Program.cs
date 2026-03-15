using Auth.Api.Auth;
using Auth.Api.Persistence;
using Auth.Api.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("AuthDb")
    ?? builder.Configuration["AUTH_DB_CONNECTION"]
    ?? "Server=localhost,1433;Database=AuthDb;User Id=sa;Password=Your_strong_Password123;TrustServerCertificate=True;";

builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddSignInManager();

builder.Services.AddScoped<TokenService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/auth/register", async (
    RegisterRequest request,
    UserManager<ApplicationUser> userManager,
    CancellationToken cancellationToken) =>
{
    var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
    var result = await userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
    {
        return Results.BadRequest(result.Errors.Select(x => x.Description));
    }

    await userManager.AddToRoleAsync(user, "admin");
    return Results.Created($"/auth/users/{user.Id}", new { user.Id, user.Email });
});

app.MapPost("/auth/login", async (
    LoginRequest request,
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext,
    TokenService tokenService,
    CancellationToken cancellationToken) =>
{
    var user = await userManager.FindByEmailAsync(request.Email);
    if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
    {
        return Results.Unauthorized();
    }

    var tokenResponse = await tokenService.GenerateTokensAsync(user, cancellationToken);
    dbContext.RefreshTokens.Add(new RefreshTokenEntity
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        Token = tokenResponse.RefreshToken,
        ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
    });
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(tokenResponse);
});

app.MapPost("/auth/refresh", async (
    RefreshTokenRequest request,
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext,
    TokenService tokenService,
    CancellationToken cancellationToken) =>
{
    var refreshToken = await dbContext.RefreshTokens
        .FirstOrDefaultAsync(x => x.Token == request.RefreshToken && !x.IsRevoked, cancellationToken);

    if (refreshToken is null || refreshToken.ExpiresAtUtc <= DateTime.UtcNow)
    {
        return Results.Unauthorized();
    }

    var user = await userManager.FindByIdAsync(refreshToken.UserId);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    refreshToken.IsRevoked = true;
    var newTokens = await tokenService.GenerateTokensAsync(user, cancellationToken);
    dbContext.RefreshTokens.Add(new RefreshTokenEntity
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        Token = newTokens.RefreshToken,
        ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
    });

    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(newTokens);
});

await SeedRolesAsync(app.Services);
app.Run();

static async Task SeedRolesAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "admin", "catalog-manager", "viewer" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
