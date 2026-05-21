using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WarehouseItem.FileService.Yc;

public class Handler
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<Handler> _logger;

    public Handler()
    {
        var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());

        var s3Config = new AmazonS3Config
        {
            ServiceURL = config["S3_SERVICE_URL"] ?? "https://storage.yandexcloud.net",
            ForcePathStyle = true
        };
        var creds = new BasicAWSCredentials(
            config["S3_ACCESS_KEY"] ?? "",
            config["S3_SECRET_KEY"] ?? "");
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(creds, s3Config));

        var provider = services.BuildServiceProvider();
        _s3 = provider.GetRequiredService<IAmazonS3>();
        _logger = provider.GetRequiredService<ILogger<Handler>>();
        _bucket = config["S3_BUCKET"] ?? "warehouse-item-files";
    }

    public async Task FunctionHandler(QueueRequest request)
    {
        _logger.LogInformation("Got {Count} messages", request.Messages.Count);

        foreach (var evt in request.Messages)
        {
            var message = evt.Details?.Message;
            if (message is null) continue;

            try
            {
                using var doc = JsonDocument.Parse(message.Body);
                var id = doc.RootElement.GetProperty("id").GetInt32();
                var key = $"warehouse_item_{id}.json";

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(message.Body));
                await _s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucket,
                    Key = key,
                    InputStream = stream,
                    ContentType = "application/json"
                });

                _logger.LogInformation("Saved {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message {Id}", message.MessageId);
            }
        }
    }
}

public class QueueRequest
{
    [JsonPropertyName("messages")]
    public List<QueueEvent> Messages { get; set; } = new();
}

public class QueueEvent
{
    [JsonPropertyName("details")]
    public QueueEventDetails? Details { get; set; }
}

public class QueueEventDetails
{
    [JsonPropertyName("message")]
    public QueueMessage? Message { get; set; }
}

public class QueueMessage
{
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";
}
