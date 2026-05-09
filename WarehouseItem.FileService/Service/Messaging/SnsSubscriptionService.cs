using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using System.Net;

namespace WarehouseItem.FileService.Service.Messaging;

/// <summary>
/// Служба для подписки FileService на SNS топик при старте приложения.
/// </summary>
public class SnsSubscriptionService(
    IAmazonSimpleNotificationService snsClient,
    IConfiguration configuration,
    ILogger<SnsSubscriptionService> logger) : ISubscriptionService
{
    private readonly string _topicArn = configuration["AWS:Resources:SNSTopicArn"] ??
                                        throw new KeyNotFoundException("SNS topic ARN was not found in configuration");

    public async Task SubscribeEndpoint()
    {
        logger.LogInformation("Sending subscribe request for topic {topic}", _topicArn);
        var endpoint = configuration["AWS:Resources:SNSUrl"];

        var request = new SubscribeRequest
        {
            TopicArn = _topicArn,
            Protocol = "http",
            Endpoint = endpoint,
            ReturnSubscriptionArn = true
        };
        var response = await snsClient.SubscribeAsync(request);

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Failed to subscribe to topic {topic}", _topicArn);
        }
        else
        {
            logger.LogInformation("Subscription request for topic {topic} succeeded, waiting for confirmation",
                _topicArn);
        }
    }
}
