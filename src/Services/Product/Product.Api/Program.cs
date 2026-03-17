using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Product.Application;
using Product.Application.Products.Commands;
using Product.Application.Products.Queries;
using Product.Infrastructure;
using Product.Infrastructure.Persistence;
using Shared.Contracts.Products;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/products", async (CreateProductRequest request, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new CreateProductCommand(request.Name, request.Price, request.Stock), cancellationToken);
    return Results.Created($"/products/{result.Id}", result);
})
.RequireAuthorization("ProductWriterPolicy");

app.MapPut("/products/{id:guid}", async (Guid id, UpdateProductRequest request, ISender sender, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await sender.Send(new UpdateProductCommand(id, request.Name, request.Price, request.Stock), cancellationToken);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { ex.Message });
    }
})
.RequireAuthorization("ProductWriterPolicy");

app.MapGet("/products", async (ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetProductsQuery(), cancellationToken);
    return Results.Ok(result);
})
.AllowAnonymous();

app.Run();
