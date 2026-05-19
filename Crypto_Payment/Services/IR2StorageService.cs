namespace Crypto_Payment.Services;

public interface IR2StorageService
{
    Task<string> UploadFileAsync(Stream stream, string fileName, string contentType);
}
