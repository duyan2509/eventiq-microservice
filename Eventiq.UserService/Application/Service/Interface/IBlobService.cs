namespace Eventiq.UserService.Application.Service;

public interface IBlobService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, string containerName = "user-avatars");
    Task DeleteAsync(string blobUrl, string containerName = "user-avatars");
}
