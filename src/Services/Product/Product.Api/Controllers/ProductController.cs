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

    public ProductController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [Authorize(Policy = "ProductWriterPolicy")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateProductCommand(request.Name, request.Price, request.Stock), cancellationToken);
        return Created($"/products/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ProductWriterPolicy")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new UpdateProductCommand(id, request.Name, request.Price, request.Stock), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetProducts(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetProductsQuery(), cancellationToken);
        return Ok(result);
    }
}
