using Amazon.S3;
using Amazon.S3.Model;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WarehouseItem.FileService.Service.Storage;

/// <summary>
/// Реализация IFileService для работы с Amazon S3.
/// </summary>
public class AwsFileService(IAmazonS3 client, IConfiguration configuration, ILogger<AwsFileService> logger)
    : IFileService
{
    private readonly string _bucketName = configuration["AWS:Resources:S3BucketName"]
        ?? throw new KeyNotFoundException("S3 bucket name was not found in configuration");

    public async Task<List<string>> GetFilesList()
    {
        var list = new List<string>();

        var request = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = "",
            Delimiter = ","
        };
        var paginator = client.Paginators.ListObjectsV2(request);

        logger.LogInformation("Listing files in bucket {bucket}.", _bucketName);
        await foreach (var response in paginator.Responses)
        {
            if (response?.S3Objects == null)
            {
                logger.LogWarning("Received null response from bucket {bucket}.", _bucketName);
                continue;
            }

            foreach (var obj in response.S3Objects)
            {
                if (obj == null)
                {
                    logger.LogWarning("Received null object from bucket {bucket}.", _bucketName);
                    continue;
                }

                list.Add(obj.Key);
            }
        }

        return list;
    }

    public async Task<bool> UploadFile(string fileData)
    {
        logger.LogInformation("Uploading file data to S3.");

        var rootNode = JsonNode.Parse(fileData) ?? throw new ArgumentException("Passed string is not a valid JSON");
        var id = rootNode["Id"]?.GetValue<int>() ?? throw new ArgumentException("Passed JSON has invalid structure");

        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, rootNode);
        stream.Seek(0, SeekOrigin.Begin);

        logger.LogInformation("Uploading warehouse item id={Id} to bucket {bucket}.", id, _bucketName);
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = $"warehouse_item_{id}.json",
            InputStream = stream
        };

        var response = await client.PutObjectAsync(request);

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Failed to upload warehouse item {id}: {code}.", id, response.HttpStatusCode);
            return false;
        }

        logger.LogInformation("Uploaded warehouse item {id} to {bucket}.", id, _bucketName);
        return true;
    }

    public async Task<JsonNode> DownloadFile(string key)
    {
        logger.LogInformation("Downloading file {file} from bucket {bucket}.", key, _bucketName);

        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };
            using var response = await client.GetObjectAsync(request);

            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                logger.LogError("Failed to download file {file}, code: {code}.", key, response.HttpStatusCode);
                throw new InvalidOperationException(
                    $"Error downloading file {key} - {response.HttpStatusCode}.");
            }

            using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
            return JsonNode.Parse(await reader.ReadToEndAsync()) ??
                   throw new InvalidOperationException($"Downloaded document is not a valid JSON.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during downloading file {file}.", key);
            throw;
        }
    }

    public async Task EnsureBucketExists()
    {
        logger.LogInformation("Checking whether bucket {bucket} exists.", _bucketName);
        try
        {
            await client.EnsureBucketExistsAsync(_bucketName);
            logger.LogInformation("Bucket {bucket} existence ensured.", _bucketName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception during checking bucket {bucket}.", _bucketName);
            throw;
        }
    }
}
