using WarehouseItem.Generator.DTO;

namespace WarehouseItem.Generator.Service.Messaging;

/// <summary>
/// Интерфейс службы для отправки сгенерированных объектов в брокер сообщений.
/// </summary>
public interface IPublisherService
{
    /// <summary>
    /// Отправляет объект товара в брокер.
    /// </summary>
    Task SendMessage(WarehouseItemDto warehouseItem);
}
