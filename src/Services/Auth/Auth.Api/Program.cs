using Auth.Api.Auth;
using Auth.Api.Logging;
using Auth.Api.Persistence;
using Auth.Api.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
var centralLogEndpoint = builder.Configuration["CENTRAL_LOG_ENDPOINT"] ?? "http://localhost:5092/logs";
builder.Logging.AddProvider(new CentralLogForwarderLoggerProvider("Auth.Api", centralLogEndpoint));

var connectionString = builder.Configuration.GetConnectionString("AuthDb")
    ?? builder.Configuration["AUTH_DB_CONNECTION"]
    ?? "Server=DESKTOP-7CMCCUI\\SQLEXPRESS2022;Database=AuthDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>();

builder.Services.AddScoped<TokenService>();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();
var requestLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Auth.Api.Requests");
var authLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Auth.Api.Business");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    var startedAt = DateTime.UtcNow;
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        requestLogger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path.Value);
        throw;
    }
    finally
    {
        var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;
        var message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms";
        if (context.Response.StatusCode >= 500)
        {
            requestLogger.LogError(message, context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsedMs);
        }
        else if (context.Response.StatusCode >= 400)
        {
            requestLogger.LogWarning(message, context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsedMs);
        }
        else
        {
            requestLogger.LogInformation(message, context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsedMs);
        }
    }
});

app.MapPost("/auth/register", async (
    RegisterRequest request,
    UserManager<ApplicationUser> userManager,
    CancellationToken cancellationToken) =>
{
    var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
    var result = await userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
    {
        authLogger.LogWarning("User registration failed for {Email}", request.Email);
        return Results.BadRequest(result.Errors.Select(x => x.Description));
    }

    await userManager.AddToRoleAsync(user, "admin");
    authLogger.LogInformation("User {Email} registered successfully with default admin role", request.Email);
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
        authLogger.LogWarning("Invalid login attempt for {Email}", request.Email);
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

    authLogger.LogInformation("User {Email} logged in and received token pair", request.Email);
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
        authLogger.LogWarning("Refresh token validation failed");
        return Results.Unauthorized();
    }

    var user = await userManager.FindByIdAsync(refreshToken.UserId);
    if (user is null)
    {
        authLogger.LogWarning("Refresh token belongs to a missing user");
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
    authLogger.LogInformation("Refresh token succeeded for user {UserId}", user.Id);
    return Results.Ok(newTokens);
});

await SeedRolesAsync(app.Services);
app.Run();

static async Task SeedRolesAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "admin", "catalog-manager", "viewer" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
