namespace WarehouseItem.FileService.Service.Messaging;

public interface ISubscriptionService
{
    /// <summary>
    /// Подписывается на SNS топик при старте приложения.
    /// </summary>
    Task SubscribeEndpoint();
}
