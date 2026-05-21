using System.Text.Json.Nodes;

namespace WarehouseItem.FileService.Service.Storage;

/// <summary>
/// Интерфейс службы для работы с файлами в объектном хранилище S3.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Сохраняет файл в хранилище.
    /// </summary>
    /// <param name="fileData">JSON-строка сохраняемого объекта</param>
    Task<bool> UploadFile(string fileData);

    /// <summary>
    /// Возвращает список всех файлов из хранилища.
    /// </summary>
    Task<List<string>> GetFilesList();

    /// <summary>
    /// Скачивает файл из хранилища и возвращает его как JsonNode.
    /// </summary>
    /// <param name="key">Ключ файла в бакете</param>
    Task<JsonNode> DownloadFile(string key);

    /// <summary>
    /// Создаёт S3 бакет при необходимости.
    /// </summary>
    Task EnsureBucketExists();
}
