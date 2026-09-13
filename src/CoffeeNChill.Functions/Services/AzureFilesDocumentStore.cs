using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Services;

// Production implementation backed by a real Azure Files share. Activated with
// Storage:Mode=AzureFiles and a real AzureWebJobsStorage connection string.
public class AzureFilesDocumentStore : IStaffDocumentStore
{
    private readonly ShareClient _share;
    private readonly ILogger<AzureFilesDocumentStore> _logger;

    public AzureFilesDocumentStore(IConfiguration config, ILogger<AzureFilesDocumentStore> logger)
    {
        _logger = logger;
        var shareName = config["Storage:AzureFilesShareName"] ?? StorageConfig.GetStaffDocsShareName(config);
        _share = new ShareClient(StorageConfig.GetConnectionString(config), shareName);
    }

    public async Task<StaffDocumentInfo> UploadAsync(string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        await _share.CreateIfNotExistsAsync(cancellationToken: ct);
        var file = _share.GetRootDirectoryClient().GetFileClient(fileName);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        await file.CreateAsync(buffer.Length, new ShareFileCreateOptions
        {
            HttpHeaders = new ShareFileHttpHeaders { ContentType = contentType }
        }, cancellationToken: ct);
        await file.UploadRangeAsync(new HttpRange(0, buffer.Length), buffer, cancellationToken: ct);

        var props = await file.GetPropertiesAsync(cancellationToken: ct);
        _logger.LogInformation("Uploaded staff document {FileName} to Azure Files ({Size} bytes)", fileName, buffer.Length);
        return new StaffDocumentInfo(fileName, props.Value.ContentLength, props.Value.LastModified, contentType);
    }

    public async Task<IReadOnlyList<StaffDocumentInfo>> ListAsync(CancellationToken ct = default)
    {
        await _share.CreateIfNotExistsAsync(cancellationToken: ct);
        var docs = new List<StaffDocumentInfo>();
        var root = _share.GetRootDirectoryClient();

        await foreach (var item in root.GetFilesAndDirectoriesAsync(cancellationToken: ct))
        {
            if (item.IsDirectory) continue;
            var props = await root.GetFileClient(item.Name).GetPropertiesAsync(cancellationToken: ct);
            docs.Add(new StaffDocumentInfo(
                item.Name,
                props.Value.ContentLength,
                props.Value.LastModified,
                props.Value.ContentType ?? "application/octet-stream"));
        }
        return docs;
    }

    public async Task<StaffDocumentDownload?> DownloadAsync(string fileName, CancellationToken ct = default)
    {
        await _share.CreateIfNotExistsAsync(cancellationToken: ct);
        var file = _share.GetRootDirectoryClient().GetFileClient(fileName);
        try
        {
            var props = await file.GetPropertiesAsync(cancellationToken: ct);
            var download = await file.DownloadAsync(cancellationToken: ct);
            return new StaffDocumentDownload(
                download.Value.Content,
                props.Value.ContentType ?? "application/octet-stream",
                download.Value.ContentLength,
                props.Value.LastModified);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
