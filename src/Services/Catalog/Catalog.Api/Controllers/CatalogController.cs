using Catalog.Api.Features.Catalog.Queries.GetCatalogItems;
using Catalog.Api.Features.Catalog.Queries.GetCatalogSagas;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly ISender _sender;

    public CatalogController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems(CancellationToken cancellationToken)
    {
        var items = await _sender.Send(new GetCatalogItemsQuery(), cancellationToken);
        return Ok(items);
    }

    [HttpGet("sagas")]
    public async Task<IActionResult> GetSagas(CancellationToken cancellationToken)
    {
        var sagas = await _sender.Send(new GetCatalogSagasQuery(), cancellationToken);
        return Ok(sagas);
    }
}
