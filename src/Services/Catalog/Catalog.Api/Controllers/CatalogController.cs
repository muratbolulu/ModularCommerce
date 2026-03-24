using Catalog.Application.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("catalog")]
public sealed class CatalogController : ControllerBase
{
    private readonly CatalogReadService _catalogReadService;

    public CatalogController(CatalogReadService catalogReadService)
    {
        _catalogReadService = catalogReadService;
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems(CancellationToken cancellationToken)
    {
        var items = await _catalogReadService.GetItemsAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("sagas")]
    public async Task<IActionResult> GetSagas(CancellationToken cancellationToken)
    {
        var sagas = await _catalogReadService.GetSagaStatesAsync(cancellationToken);
        return Ok(sagas);
    }
}
