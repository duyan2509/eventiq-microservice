using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Eventiq.EventService.Application.Service;

namespace Eventiq.EventService.Infrastructure.Blob;

public class AzureBlobService : IBlobService
{
    private readonly string _connectionString;
    private readonly ILogger<AzureBlobService> _logger;

    public AzureBlobService(IConfiguration configuration, ILogger<AzureBlobService> logger)
    {
        _connectionString = configuration["AzureBlob:ConnectionString"]
            ?? throw new InvalidOperationException("AzureBlob:ConnectionString is not configured.");
        _logger = logger;
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, string containerName = "event-banners")
    {
        var blobServiceClient = new BlobServiceClient(_connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        // Ensure container exists and is publicly accessible
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        // Generate unique blob name to avoid collisions
        var ext = Path.GetExtension(fileName);
        var blobName = $"{Guid.NewGuid()}{ext}";
        var blobClient = containerClient.GetBlobClient(blobName);

        stream.Position = 0;
        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType });

        _logger.LogInformation("Uploaded blob {BlobName} to container {Container}", blobName, containerName);

        return blobClient.Uri.ToString();
    }

    public async Task DeleteAsync(string blobUrl, string containerName = "event-banners")
    {
        if (string.IsNullOrWhiteSpace(blobUrl)) return;

        try
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Extract blob name from URL
            var uri = new Uri(blobUrl);
            var blobName = uri.Segments.Last();
            var blobClient = containerClient.GetBlobClient(blobName);

            await blobClient.DeleteIfExistsAsync();
            _logger.LogInformation("Deleted blob {BlobName} from container {Container}", blobName, containerName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete blob {BlobUrl}", blobUrl);
        }
    }
}
