using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Services;

// Azurite emulates Blob, Queue and Table storage only - it does not emulate the
// Azure Files service. For local development the staff-docs share is therefore
// backed by a Blob container named "staff-docs", preserving identical
// streaming/metadata semantics. Set Storage:Mode=AzureFiles to swap in the real
// Azure Files Share implementation without touching the HTTP layer.
public class AzuriteDocumentStore : IStaffDocumentStore
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<AzuriteDocumentStore> _logger;

    public AzuriteDocumentStore(IConfiguration config, ILogger<AzuriteDocumentStore> logger)
    {
        _logger = logger;
        _container = new BlobContainerClient(
            StorageConfig.GetConnectionString(config),
            StorageConfig.GetStaffDocsShareName(config));
    }

    public async Task<StaffDocumentInfo> UploadAsync(string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = _container.GetBlobClient(fileName);

        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = new Dictionary<string, string>
            {
                ["originalfilename"] = fileName,
                ["uploadedutc"] = DateTimeOffset.UtcNow.ToString("O")
            }
        }, ct);

        var props = await blob.GetPropertiesAsync(cancellationToken: ct);
        _logger.LogInformation("Uploaded staff document {FileName} ({Size} bytes)", fileName, props.Value.ContentLength);
        return new StaffDocumentInfo(fileName, props.Value.ContentLength, props.Value.LastModified, contentType);
    }

    public async Task<IReadOnlyList<StaffDocumentInfo>> ListAsync(CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        var docs = new List<StaffDocumentInfo>();
        await foreach (var item in _container.GetBlobsAsync(cancellationToken: ct))
        {
            docs.Add(new StaffDocumentInfo(
                item.Name,
                item.Properties.ContentLength ?? 0,
                item.Properties.LastModified ?? DateTimeOffset.MinValue,
                item.Properties.ContentType ?? "application/octet-stream"));
        }
        return docs;
    }

    public async Task<StaffDocumentDownload?> DownloadAsync(string fileName, CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = _container.GetBlobClient(fileName);
        try
        {
            var result = await blob.DownloadStreamingAsync(cancellationToken: ct);
            return new StaffDocumentDownload(
                result.Value.Content,
                result.Value.Details.ContentType ?? "application/octet-stream",
                result.Value.Details.ContentLength,
                result.Value.Details.LastModified);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
