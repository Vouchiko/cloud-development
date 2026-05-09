using WarehouseItem.Generator.DTO;
using WarehouseItem.Generator.Generator;
using WarehouseItem.Generator.Service.Messaging;

namespace WarehouseItem.Generator.Service;

/// <summary>
/// Реализация сервиса работы с товарами на складе.
/// </summary>
public sealed class WarehouseItemService(
    WarehouseItemGenerator generator,
    IWarehouseItemCache cache,
    IPublisherService publisherService) : IWarehouseItemService
{
    /// <summary>
    /// Получить товар по идентификатору. Если товар не найден в кэше, генерирует новый,
    /// сохраняет в кэш и отправляет в SNS для записи в S3.
    /// </summary>
    public async Task<WarehouseItemDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetAsync(id, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var generated = generator.Generate(id);
        await cache.SetAsync(id, generated, cancellationToken);
        await publisherService.SendMessage(generated);

        return generated;
    }
}
