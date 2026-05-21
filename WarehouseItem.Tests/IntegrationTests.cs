using Aspire.Hosting;
using System.Text.Json;
using WarehouseItem.Generator.DTO;

namespace WarehouseItem.Tests;

/// <summary>
/// Интеграционные тесты для проверки микросервисного пайплайна.
/// </summary>
/// <param name="fixture">Фикстура для запуска Aspire один раз для всех тестов класса.</param>
public class IntegrationTests(AppFixture fixture) : IClassFixture<AppFixture>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _jsonOptions);

    private readonly DistributedApplication _app = fixture.App;

    /// <summary>
    /// Проверяет основной положительный сценарий:
    /// запрос через Gateway, генерация, сохранение в S3, данные идентичны.
    /// </summary>
    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    public async Task GetFromGateway_SavesToS3_AndDataIsIdentical(int id)
    {
        var gatewayClient = _app.CreateHttpClient("gateway", "http");
        var fileClient = _app.CreateHttpClient("warehouse-item-file-service", "http");

        var response = await gatewayClient.GetAsync($"/warehouse-item?id={id}");
        response.EnsureSuccessStatusCode();

        var apiItem = Deserialize<WarehouseItemDto>(await response.Content.ReadAsStringAsync());

        await WaitForFileInS3Async(fileClient, id, TimeSpan.FromSeconds(10));

        var s3Item = await GetItemFromS3Async(fileClient, id);

        Assert.NotNull(apiItem);
        Assert.Equal(id, apiItem.Id);
        Assert.NotNull(s3Item);
        Assert.Equal(id, s3Item.Id);
        Assert.Equivalent(apiItem, s3Item, strict: true);
    }

    /// <summary>
    /// Проверяет работу кэширования:
    /// при повторных запросах одного id возвращается идентичный объект,
    /// и S3 также возвращает тот же объект.
    /// </summary>
    [Fact]
    public async Task SameIds_GivingSameObjects()
    {
        var gatewayClient = _app.CreateHttpClient("gateway", "http");
        var fileClient = _app.CreateHttpClient("warehouse-item-file-service", "http");

        var id = Random.Shared.Next(20, 30);

        var response = await gatewayClient.GetAsync($"/warehouse-item?id={id}");
        response.EnsureSuccessStatusCode();

        var apiItem = Deserialize<WarehouseItemDto>(await response.Content.ReadAsStringAsync());

        await WaitForFileInS3Async(fileClient, id, TimeSpan.FromSeconds(10));

        var s3Item = await GetItemFromS3Async(fileClient, id);

        var response1 = await gatewayClient.GetAsync($"/warehouse-item?id={id}");
        response1.EnsureSuccessStatusCode();

        var apiItem1 = Deserialize<WarehouseItemDto>(await response1.Content.ReadAsStringAsync());

        await WaitForFileInS3Async(fileClient, id, TimeSpan.FromSeconds(10));

        var s3Item1 = await GetItemFromS3Async(fileClient, id);

        Assert.NotNull(apiItem);
        Assert.Equal(id, apiItem.Id);
        Assert.NotNull(s3Item);
        Assert.Equal(id, s3Item.Id);
        Assert.Equivalent(apiItem, s3Item, strict: true);

        Assert.NotNull(apiItem1);
        Assert.Equal(id, apiItem1.Id);
        Assert.NotNull(s3Item1);
        Assert.Equal(id, s3Item1.Id);
        Assert.Equivalent(apiItem1, s3Item1, strict: true);

        Assert.Equivalent(apiItem, apiItem1, strict: true);
        Assert.Equivalent(s3Item, s3Item1, strict: true);
    }

    /// <summary>
    /// Проверяет эндпоинт получения списка файлов в S3 и уникальность по Id.
    /// </summary>
    [Fact]
    public async Task S3_ReturnsCorrectListOfFiles()
    {
        var gatewayClient = _app.CreateHttpClient("gateway", "http");
        var fileClient = _app.CreateHttpClient("warehouse-item-file-service", "http");

        var ids = new[] { 1001, 1002, 1003, 1001 };

        foreach (var id in ids)
        {
            await gatewayClient.GetAsync($"/warehouse-item?id={id}");
            await WaitForFileInS3Async(fileClient, id, TimeSpan.FromSeconds(10));
        }

        var listResponse = await fileClient.GetAsync("/api/s3");
        listResponse.EnsureSuccessStatusCode();

        var fileList = Deserialize<List<string>>(await listResponse.Content.ReadAsStringAsync());

        Assert.NotNull(fileList);

        foreach (var id in ids)
        {
            Assert.Contains($"warehouse_item_{id}.json", fileList);
        }

        var addedFiles = fileList.Where(f => f.StartsWith("warehouse_item_100")).ToList();
        Assert.Equal(3, addedFiles.Count);
    }

    /// <summary>
    /// Проверяет обработку некорректных значений id в Gateway.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("asd")]
    public async Task InvalidId_ReturnsBadRequest(string invalidId)
    {
        var gatewayClient = _app.CreateHttpClient("gateway", "http");

        var response = await gatewayClient.GetAsync($"/warehouse-item?id={invalidId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Проверяет поведение при отсутствии параметра id.
    /// </summary>
    [Fact]
    public async Task MissingIdParameter_ReturnsBadRequest()
    {
        var gatewayClient = _app.CreateHttpClient("gateway", "http");

        var response = await gatewayClient.GetAsync("/warehouse-item");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Проверяет, что при некорректном id файл в S3 не создаётся.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("asd")]
    public async Task InvalidId_DoesNotCreateFileInS3(string invalidId)
    {
        var gatewayClient = _app.CreateHttpClient("gateway", "http");
        var fileClient = _app.CreateHttpClient("warehouse-item-file-service", "http");

        var response = await gatewayClient.GetAsync($"/warehouse-item?id={invalidId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await Task.Delay(5000);

        var fileName = $"warehouse_item_{invalidId}.json";
        var probe = await fileClient.GetAsync($"/api/s3/{fileName}");

        Assert.False(probe.IsSuccessStatusCode, "Файл не должен был появиться в S3 при невалидном id");
    }

    /// <summary>
    /// Ожидает появления файла в S3 с заданным id
    /// </summary>
    private static async Task WaitForFileInS3Async(HttpClient fileClient, int id, TimeSpan timeout)
    {
        var fileName = $"warehouse_item_{id}.json";
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var probe = await fileClient.GetAsync($"/api/s3/{fileName}");
            if (probe.IsSuccessStatusCode)
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"File {fileName} did not appear in S3 within {timeout.TotalSeconds}s");
    }

    /// <summary>
    /// Скачивает и десериализует WarehouseItemDto из S3 через FileService.
    /// </summary>
    private static async Task<WarehouseItemDto> GetItemFromS3Async(HttpClient fileClient, int id)
    {
        var fileServiceResponse = await fileClient.GetAsync($"/api/s3/warehouse_item_{id}.json");
        return Deserialize<WarehouseItemDto>(await fileServiceResponse.Content.ReadAsStringAsync())!;
    }
}
