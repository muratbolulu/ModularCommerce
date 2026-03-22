using Auth.Api.Auth;
using Auth.Api.Logging;
using Auth.Api.Persistence;
using Auth.Api.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
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

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

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
