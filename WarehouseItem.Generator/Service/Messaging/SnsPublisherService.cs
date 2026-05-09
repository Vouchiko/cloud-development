using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using System.Net;
using System.Text.Json;
using WarehouseItem.Generator.DTO;

namespace WarehouseItem.Generator.Service.Messaging;

/// <summary>
/// Служба публикации товаров в SNS топик.
/// </summary>
public class SnsPublisherService(
    IAmazonSimpleNotificationService client,
    IConfiguration configuration,
    ILogger<SnsPublisherService> logger) : IPublisherService
{
    private readonly string _topicArn = configuration["AWS:Resources:SNSTopicArn"]
                                        ?? throw new KeyNotFoundException(
                                            "SNS topic ARN was not found in configuration");

    public async Task SendMessage(WarehouseItemDto warehouseItem)
    {
        try
        {
            var json = JsonSerializer.Serialize(warehouseItem);
            var request = new PublishRequest
            {
                Message = json,
                TopicArn = _topicArn
            };

            var statusCode = (await client.PublishAsync(request)).HttpStatusCode;
            if (statusCode != HttpStatusCode.OK)
            {
                throw new Exception($"SNS returned status code {statusCode}");
            }

            logger.LogInformation("Warehouse item id={Id} sent to file service via SNS", warehouseItem.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to send warehouse item through SNS topic");
        }
    }
}
