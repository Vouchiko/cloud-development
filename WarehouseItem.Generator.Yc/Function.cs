using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WarehouseItem.Generator.Yc;

public class Handler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAmazonSQS _sqs;
    private readonly IAmazonS3 _s3;
    private readonly string _queueUrl;
    private readonly string _bucket;
    private readonly ILogger<Handler> _logger;

    public Handler()
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddSingleton<IConfiguration>(config);

        var creds = new BasicAWSCredentials(
            config["SQS_ACCESS_KEY"] ?? "",
            config["SQS_SECRET_KEY"] ?? "");

        var sqsConfig = new AmazonSQSConfig
        {
            ServiceURL = config["SQS_SERVICE_URL"] ?? "https://message-queue.api.cloud.yandex.net",
            AuthenticationRegion = "ru-central1"
        };
        services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(creds, sqsConfig));

        var s3Config = new AmazonS3Config
        {
            ServiceURL = config["S3_SERVICE_URL"] ?? "https://storage.yandexcloud.net",
            ForcePathStyle = true
        };
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(creds, s3Config));

        var provider = services.BuildServiceProvider();
        _sqs = provider.GetRequiredService<IAmazonSQS>();
        _s3 = provider.GetRequiredService<IAmazonS3>();
        _logger = provider.GetRequiredService<ILogger<Handler>>();
        _queueUrl = config["SQS_QUEUE_URL"]
            ?? throw new InvalidOperationException("SQS_QUEUE_URL is not set");
        _bucket = config["S3_BUCKET"] ?? "warehouse-item-files";
    }

    public async Task<Response> FunctionHandler(Request request)
    {
        if (request.QueryStringParameters is null
            || !request.QueryStringParameters.TryGetValue("id", out var idStr)
            || !int.TryParse(idStr, out var id)
            || id <= 0)
        {
            return new Response { StatusCode = 400, Body = "id must be a positive integer" };
        }

        var key = $"warehouse_item_{id}.json";

        var cachedJson = await TryReadFromBucketAsync(key);
        if (cachedJson is not null)
        {
            _logger.LogInformation("Cache hit in bucket for id={Id}", id);
            return BuildJsonResponse(cachedJson);
        }

        var item = WarehouseItemGenerator.Generate(id);
        var json = JsonSerializer.Serialize(item, JsonOptions);

        try
        {
            await _sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = json
            });
            _logger.LogInformation("Warehouse item id={Id} sent to YMQ", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to YMQ");
        }

        return BuildJsonResponse(json);
    }

    private async Task<string?> TryReadFromBucketAsync(string key)
    {
        try
        {
            using var response = await _s3.GetObjectAsync(_bucket, key);
            using var reader = new StreamReader(response.ResponseStream);
            return await reader.ReadToEndAsync();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read {Key} from bucket", key);
            return null;
        }
    }

    private static Response BuildJsonResponse(string body) => new()
    {
        StatusCode = 200,
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Access-Control-Allow-Origin"] = "*"
        },
        Body = body
    };
}

public class Request
{
    [JsonPropertyName("httpMethod")]
    public string? HttpMethod { get; set; }

    [JsonPropertyName("queryStringParameters")]
    public Dictionary<string, string>? QueryStringParameters { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }
}

public class Response
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";
}
