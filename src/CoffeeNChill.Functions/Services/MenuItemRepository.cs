using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Services;

public class MenuItemRepository : IMenuItemRepository
{
    private readonly TableClient _table;
    private readonly ILogger<MenuItemRepository> _logger;

    public MenuItemRepository(IConfiguration config, ILogger<MenuItemRepository> logger)
    {
        _logger = logger;
        var client = new TableServiceClient(StorageConfig.GetConnectionString(config));
        _table = client.GetTableClient(StorageConfig.GetMenuTableName(config));
    }

    private async Task EnsureTableAsync(CancellationToken ct) =>
        await _table.CreateIfNotExistsAsync(ct);

    public async Task<MenuItemEntity> CreateAsync(MenuItemEntity entity, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        try
        {
            await _table.AddEntityAsync(entity, ct);
            _logger.LogInformation("Created menu item {Sku} in category {Category}", entity.RowKey, entity.PartitionKey);
            return entity;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new StorageConflictException($"A menu item with id '{entity.RowKey}' already exists in category '{entity.PartitionKey}'.");
        }
    }

    public async Task<IReadOnlyList<MenuItemEntity>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        var items = new List<MenuItemEntity>();
        await foreach (var entity in _table.QueryAsync<MenuItemEntity>(cancellationToken: ct))
            items.Add(entity);
        return items;
    }

    public async Task<IReadOnlyList<MenuItemEntity>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        var items = new List<MenuItemEntity>();
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {category}");
        await foreach (var entity in _table.QueryAsync<MenuItemEntity>(filter, cancellationToken: ct))
            items.Add(entity);
        return items;
    }

    public async Task<MenuItemEntity?> GetAsync(string category, string id, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct);
        try
        {
            var response = await _table.GetEntityAsync<MenuItemEntity>(category, id, cancellationToken: ct);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<MenuItemEntity?> UpdateAsync(string category, string id, double? price, bool? isAvailable, CancellationToken ct = default)
    {
        var entity = await GetAsync(category, id, ct);
        if (entity is null)
            return null;

        if (price.HasValue) entity.Price = price.Value;
        if (isAvailable.HasValue) entity.IsAvailable = isAvailable.Value;

        await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);
        _logger.LogInformation("Updated menu item {Sku} in category {Category}", id, category);
        return entity;
    }

    public async Task<bool> DeleteAsync(string category, string id, CancellationToken ct = default)
    {
        var existing = await GetAsync(category, id, ct);
        if (existing is null)
            return false;

        await _table.DeleteEntityAsync(category, id, ETag.All, ct);
        _logger.LogInformation("Deleted menu item {Sku} from category {Category}", id, category);
        return true;
    }
}
