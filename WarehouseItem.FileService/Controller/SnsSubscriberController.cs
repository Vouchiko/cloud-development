using Amazon.SimpleNotificationService.Util;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using WarehouseItem.FileService.Service.Storage;

namespace WarehouseItem.FileService.Controller;

[ApiController]
[Route("api/sns")]
public class SnsSubscriberController(IFileService fileService, ILogger<SnsSubscriberController> logger) : ControllerBase
{
    /// <summary>
    /// Вебхук для приема оповещений от SNS топика.
    /// Используется также для подтверждения подписки при инициализации.
    /// Всегда возвращает 200.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ReceiveMessage()
    {
        logger.LogInformation("SNS webhook was called.");
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var jsonContent = await reader.ReadToEndAsync();

            var snsMessage = Message.ParseMessage(jsonContent);

            switch (snsMessage.Type)
            {
                case "SubscriptionConfirmation":
                {
                    logger.LogInformation("SubscriptionConfirmation was received.");

                    using var httpClient = new HttpClient();
                    var builder = new UriBuilder(new Uri(snsMessage.SubscribeURL))
                    {
                        Scheme = "http",
                        Host = "localhost",
                        Port = 4566
                    };
                    var response = await httpClient.GetAsync(builder.Uri);

                    if (!response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        throw new Exception($"SubscriptionConfirmation returned {response.StatusCode}: {body}.");
                    }

                    logger.LogInformation("Subscription was successfully confirmed.");
                    break;
                }
                case "Notification":
                {
                    await fileService.UploadFile(snsMessage.MessageText);

                    logger.LogInformation("Notification was successfully processed.");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while processing SNS notifications.");
        }

        return Ok();
    }
}
