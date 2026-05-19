using Amazon.S3;
using Amazon.S3.Model;
using Crypto_Payment.Services;

namespace Crypto_Payment.Manager;

public class R2StorageManager : IR2StorageService
{
    private readonly IAmazonS3? _s3;
    private readonly string _bucket;
    private readonly string _publicUrl;
    private readonly bool _configured;

    public R2StorageManager()
    {
        var accountId = Environment.GetEnvironmentVariable("R2_ACCOUNT_ID") ?? "";
        var accessKey = Environment.GetEnvironmentVariable("R2_ACCESS_KEY") ?? "";
        var secretKey = Environment.GetEnvironmentVariable("R2_SECRET_KEY") ?? "";
        _bucket = Environment.GetEnvironmentVariable("R2_BUCKET_NAME") ?? "chat-files";
        _publicUrl = (Environment.GetEnvironmentVariable("R2_PUBLIC_URL") ?? "").TrimEnd('/');

        _configured = !string.IsNullOrEmpty(accountId) && !string.IsNullOrEmpty(accessKey);

        if (_configured)
        {
            var config = new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true
            };
            _s3 = new AmazonS3Client(accessKey, secretKey, config);
        }
    }

    public async Task<string> UploadFileAsync(Stream stream, string fileName, string contentType)
    {
        if (!_configured || _s3 == null)
            throw new InvalidOperationException("Cloudflare R2 yapılandırılmamış. R2_ACCOUNT_ID, R2_ACCESS_KEY ve R2_SECRET_KEY ortam değişkenlerini ayarlayın.");

        var key = $"chat/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}_{fileName}";

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            DisablePayloadSigning = true
        });

        return string.IsNullOrEmpty(_publicUrl)
            ? $"https://{_bucket}.r2.dev/{key}"
            : $"{_publicUrl}/{key}";
    }
}
