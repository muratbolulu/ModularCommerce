using System.Threading.RateLimiting;
using Gateway.Api.Logging;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
var centralLogEndpoint = builder.Configuration["CENTRAL_LOG_ENDPOINT"] ?? "http://localhost:5092/logs";
builder.Logging.AddProvider(new CentralLogForwarderLoggerProvider("Gateway.Api", centralLogEndpoint));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromSeconds(10),
                QueueLimit = 2,
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
