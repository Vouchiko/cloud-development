using Microsoft.AspNetCore.Mvc;
using WarehouseItem.Generator.DTO;
using WarehouseItem.Generator.Service;

namespace WarehouseItem.Generator.Controller;

/// <summary>
/// API контроллер для работы с товарами склада.
/// </summary>
[ApiController]
[Route("api/warehouse-item")]
public sealed class WarehouseItemController(ILogger<WarehouseItemController> logger, IWarehouseItemService service) : ControllerBase
{
    /// <summary>
    /// Получить товар по id.
    /// </summary>
    /// <param name="id">Идентификатор товара</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Данные товара</returns>
    [HttpGet]
    [ProducesResponseType(typeof(WarehouseItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WarehouseItemDto>> Get([FromQuery] int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "id must be greater than 0" });
        }

        logger.LogInformation("Request warehouse item id={id}.", id);
        var dto = await service.GetAsync(id, cancellationToken);
        logger.LogInformation("Response warehouse item id={id}.", id);

        return Ok(dto);
    }
}
