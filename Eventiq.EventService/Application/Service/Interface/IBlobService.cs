namespace Eventiq.EventService.Application.Service;

public interface IBlobService
{
    /// <summary>
    /// Uploads a file stream to Azure Blob Storage and returns the public URL.
    /// </summary>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, string containerName = "event-banners");

    /// <summary>
    /// Deletes a blob by its full URL or blob name.
    /// </summary>
    Task DeleteAsync(string blobUrl, string containerName = "event-banners");
}
