using System.Net;
using CoffeeNChill.Functions.Helpers;
using CoffeeNChill.Functions.Services;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

public class DocumentFunctions
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".txt", ".md", ".xlsx", ".pptx", ".png", ".jpg", ".jpeg"
    };

    private readonly IStaffDocumentStore _store;
    private readonly long _maxUploadBytes;
    private readonly ILogger<DocumentFunctions> _logger;

    public DocumentFunctions(IStaffDocumentStore store, IConfiguration config, ILogger<DocumentFunctions> logger)
    {
        _store = store;
        _maxUploadBytes = StorageConfig.GetMaxUploadBytes(config);
        _logger = logger;
    }

    [Function("UploadStaffDocument")]
    public async Task<HttpResponseData> UploadStaffDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "documents/upload")] HttpRequestData req,
        CancellationToken ct)
    {
        if (!req.Headers.TryGetValues("Content-Type", out var contentTypes) ||
            !contentTypes.Any(c => c.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase)))
        {
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest,
                "Content-Type must be multipart/form-data with a file field.");
        }

        MultipartFormDataParser parsed;
        try
        {
            parsed = await MultipartFormDataParser.ParseAsync(req.Body, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse multipart body");
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest, "Could not parse multipart/form-data body.");
        }

        var file = parsed.Files.FirstOrDefault();
        if (file is null)
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest,
                "No file found. Send the document as a multipart/form-data file field (e.g. 'file').");

        var fileName = Path.GetFileName(file.FileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest, "Uploaded file has no file name.");

        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest,
                $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}.");

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        if (file.Data.CanSeek && file.Data.Length > _maxUploadBytes)
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest,
                $"File exceeds the maximum upload size of {_maxUploadBytes / (1024 * 1024)} MB.");

        var info = await _store.UploadAsync(fileName, file.Data, contentType, ct);
        return await HttpHelpers.JsonAsync(req, HttpStatusCode.Created, info);
    }

    [Function("ListStaffDocuments")]
    public async Task<HttpResponseData> ListStaffDocuments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents")] HttpRequestData req,
        CancellationToken ct)
    {
        var docs = await _store.ListAsync(ct);
        return await HttpHelpers.JsonAsync(req, HttpStatusCode.OK, docs);
    }

    [Function("DownloadStaffDocument")]
    public async Task<HttpResponseData> DownloadStaffDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "documents/download/{fileName}")] HttpRequestData req,
        string fileName,
        CancellationToken ct)
    {
        var safeName = Path.GetFileName(Uri.UnescapeDataString(fileName));
        var download = await _store.DownloadAsync(safeName, ct);
        if (download is null)
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.NotFound, $"Document '{safeName}' was not found in staff-docs.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", download.ContentType);
        response.Headers.Add("Content-Disposition", $"attachment; filename=\"{safeName}\"");
        response.Headers.Add("Content-Length", download.ContentLength.ToString());
        await download.Content.CopyToAsync(response.Body, ct);
        return response;
    }
}
