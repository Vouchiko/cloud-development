using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly string _queueUrl;
    private readonly IMemoryCache _cache;
    private readonly ILogger<Handler> _logger;

    public Handler()
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddMemoryCache();
        services.AddSingleton<IConfiguration>(config);

        var sqsConfig = new AmazonSQSConfig
        {
            ServiceURL = config["SQS_SERVICE_URL"] ?? "https://message-queue.api.cloud.yandex.net",
            AuthenticationRegion = "ru-central1"
        };
        var creds = new BasicAWSCredentials(
            config["SQS_ACCESS_KEY"] ?? "",
            config["SQS_SECRET_KEY"] ?? "");
        services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(creds, sqsConfig));

        var provider = services.BuildServiceProvider();
        _sqs = provider.GetRequiredService<IAmazonSQS>();
        _cache = provider.GetRequiredService<IMemoryCache>();
        _logger = provider.GetRequiredService<ILogger<Handler>>();
        _queueUrl = config["SQS_QUEUE_URL"]
            ?? throw new InvalidOperationException("SQS_QUEUE_URL is not set");
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

        var item = await _cache.GetOrCreateAsync($"wh-{id}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return Task.FromResult(WarehouseItemGenerator.Generate(id));
        });

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

        return new Response
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Access-Control-Allow-Origin"] = "*"
            },
            Body = json
        };
    }
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
