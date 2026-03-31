using System.Threading.RateLimiting;
using Gateway.Api.Logging;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
var centralLogEndpoint = builder.Configuration["CENTRAL_LOG_ENDPOINT"] ?? "http://localhost:5092/logs";
builder.Logging.AddProvider(new CentralLogForwarderLoggerProvider("Gateway.Api", centralLogEndpoint));
var rateLimitServicesSection = builder.Configuration.GetSection("RateLimiting:Services");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var serviceName = ResolveServiceName(context.Request.Path.Value);
        var servicePolicy = ReadServicePolicy(rateLimitServicesSection, serviceName);
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        var key = $"{serviceName}:{clientIp}";

        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = servicePolicy.PermitLimit,
                Window = TimeSpan.FromSeconds(servicePolicy.WindowSeconds),
                QueueLimit = servicePolicy.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(transformContext =>
        {
            transformContext.ProxyRequest.Headers.Remove("X-Forwarded-For");
            transformContext.ProxyRequest.Headers.Add("X-Gateway", "modular-commerce-gateway");
            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();
var requestLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Gateway.Api.Requests");

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

app.UseRateLimiter();
app.MapReverseProxy();
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", UtcNow = DateTime.UtcNow }));

app.Run();

static string ResolveServiceName(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return "default";
    }

    if (path.StartsWith("/auth", StringComparison.OrdinalIgnoreCase))
    {
        return "auth";
    }

    if (path.StartsWith("/products", StringComparison.OrdinalIgnoreCase))
    {
        return "products";
    }

    if (path.StartsWith("/catalog", StringComparison.OrdinalIgnoreCase))
    {
        return "catalog";
    }

    if (path.StartsWith("/logs", StringComparison.OrdinalIgnoreCase))
    {
        return "logs";
    }

    return "default";
}

static ServiceRateLimitPolicy ReadServicePolicy(IConfigurationSection servicesSection, string serviceName)
{
    var section = servicesSection.GetSection(serviceName);
    var fallback = servicesSection.GetSection("default");

    return new ServiceRateLimitPolicy(
        PermitLimit: ReadInt(section, fallback, "PermitLimit", 20),
        WindowSeconds: ReadInt(section, fallback, "WindowSeconds", 10),
        QueueLimit: ReadInt(section, fallback, "QueueLimit", 2));
}

static int ReadInt(IConfigurationSection section, IConfigurationSection fallback, string key, int defaultValue)
{
    var value = section.GetValue<int?>(key) ?? fallback.GetValue<int?>(key);
    return value.HasValue && value.Value >= 0 ? value.Value : defaultValue;
}

internal sealed record ServiceRateLimitPolicy(int PermitLimit, int WindowSeconds, int QueueLimit);
