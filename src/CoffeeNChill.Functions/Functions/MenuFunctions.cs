using System.Net;
using CoffeeNChill.Functions.Helpers;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

public class MenuFunctions
{
    private readonly IMenuItemRepository _repository;
    private readonly ILogger<MenuFunctions> _logger;

    public MenuFunctions(IMenuItemRepository repository, ILogger<MenuFunctions> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [Function("CreateMenuItem")]
    public async Task<HttpResponseData> CreateMenuItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await HttpHelpers.ReadJsonAsync<CreateMenuItemRequest>(req);
        if (body is null)
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest, "Request body must be valid JSON.");

        if (!HttpHelpers.TryValidate(body, out var errors))
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest, "Validation failed.", errors);

        var entity = new MenuItemEntity
        {
            PartitionKey = body.Category.Trim(),
            RowKey = body.Sku.Trim(),
            Name = body.Name.Trim(),
            Description = body.Description?.Trim() ?? string.Empty,
            Price = body.Price,
            IsAvailable = body.IsAvailable
        };

        try
        {
            var created = await _repository.CreateAsync(entity, ct);
            var response = await HttpHelpers.JsonAsync(req, HttpStatusCode.Created, created.ToResponse());
            response.Headers.Add("Location", $"/api/menu/{Uri.EscapeDataString(created.PartitionKey)}/{Uri.EscapeDataString(created.RowKey)}");
            return response;
        }
        catch (StorageConflictException ex)
        {
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.Conflict, ex.Message);
        }
    }

    [Function("GetAllMenuItems")]
    public async Task<HttpResponseData> GetAllMenuItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")] HttpRequestData req,
        CancellationToken ct)
    {
        var items = await _repository.GetAllAsync(ct);
        return await HttpHelpers.JsonAsync(req, HttpStatusCode.OK, items.Select(i => i.ToResponse()).ToList());
    }

    [Function("GetMenuItemsByCategory")]
    public async Task<HttpResponseData> GetMenuItemsByCategory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/category/{category}")] HttpRequestData req,
        string category,
        CancellationToken ct)
    {
        var items = await _repository.GetByCategoryAsync(Uri.UnescapeDataString(category), ct);
        return await HttpHelpers.JsonAsync(req, HttpStatusCode.OK, items.Select(i => i.ToResponse()).ToList());
    }

    [Function("UpdateMenuItem")]
    public async Task<HttpResponseData> UpdateMenuItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu/{category}/{id}")] HttpRequestData req,
        string category,
        string id,
        CancellationToken ct)
    {
        var body = await HttpHelpers.ReadJsonAsync<UpdateMenuItemRequest>(req);
        if (body is null)
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest, "Request body must be valid JSON.");

        if (!HttpHelpers.TryValidate(body, out var errors))
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest, "Validation failed.", errors);

        if (!body.HasChanges)
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest, "Provide at least one of 'price' or 'isAvailable' to update.");

        var updated = await _repository.UpdateAsync(Uri.UnescapeDataString(category), Uri.UnescapeDataString(id), body.Price, body.IsAvailable, ct);
        if (updated is null)
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.NotFound, $"Menu item '{id}' was not found in category '{category}'.");

        return await HttpHelpers.JsonAsync(req, HttpStatusCode.OK, updated.ToResponse());
    }

    [Function("DeleteMenuItem")]
    public async Task<HttpResponseData> DeleteMenuItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu/{category}/{id}")] HttpRequestData req,
        string category,
        string id,
        CancellationToken ct)
    {
        var deleted = await _repository.DeleteAsync(Uri.UnescapeDataString(category), Uri.UnescapeDataString(id), ct);
        if (!deleted)
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.NotFound, $"Menu item '{id}' was not found in category '{category}'.");

        var response = req.CreateResponse(HttpStatusCode.NoContent);
        return response;
    }
}
