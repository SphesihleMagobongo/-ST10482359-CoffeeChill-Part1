namespace CoffeeNChill.Functions.Services;

public record StaffDocumentInfo(string FileName, long SizeBytes, DateTimeOffset LastModified, string ContentType);

public record StaffDocumentDownload(Stream Content, string ContentType, long ContentLength, DateTimeOffset LastModified);

public interface IStaffDocumentStore
{
    Task<StaffDocumentInfo> UploadAsync(string fileName, Stream content, string contentType, CancellationToken ct = default);
    Task<IReadOnlyList<StaffDocumentInfo>> ListAsync(CancellationToken ct = default);
    Task<StaffDocumentDownload?> DownloadAsync(string fileName, CancellationToken ct = default);
}
