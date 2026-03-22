using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.Application.Products.Commands;
using Product.Application.Products.Queries;
using Shared.Contracts.Products;

namespace Product.Api.Controllers;

[ApiController]
[Route("products")]
public sealed class ProductController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<ProductController> _logger;

    public ProductController(ISender sender, ILogger<ProductController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Policy = "ProductWriterPolicy")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Create product request received for {Name}", request.Name);
        var result = await _sender.Send(new CreateProductCommand(request.Name, request.Price, request.Stock), cancellationToken);
        return Created($"/products/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ProductWriterPolicy")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Update product request received for {ProductId}", id);
            var result = await _sender.Send(new UpdateProductCommand(id, request.Name, request.Price, request.Stock), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Product {ProductId} could not be updated because it does not exist", id);
            return NotFound(new { ex.Message });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetProducts(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Get products request received");
        var result = await _sender.Send(new GetProductsQuery(), cancellationToken);
        return Ok(result);
    }
}
