using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Product.Api.Logging;
using Product.Application;
using Product.Infrastructure;
using Product.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
var centralLogEndpoint = builder.Configuration["CENTRAL_LOG_ENDPOINT"] ?? "http://localhost:5092/logs";
builder.Logging.AddProvider(new CentralLogForwarderLoggerProvider("Product.Api", centralLogEndpoint));
builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var issuer = builder.Configuration["JWT_ISSUER"] ?? "modular-commerce";
var audience = builder.Configuration["JWT_AUDIENCE"] ?? "modular-commerce-clients";
var signingKey = builder.Configuration["JWT_SIGNING_KEY"] ?? "development-signing-key-please-change";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ProductWriterPolicy", policy =>
    {
        policy.RequireClaim(ClaimTypes.Role, "admin", "catalog-manager");
    });
});

var app = builder.Build();
var requestLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Product.Api.Requests");

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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
