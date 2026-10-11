using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Services;

public class OrderRepository : IOrderRepository
{
    private readonly TableClient _table;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(IConfiguration config, ILogger<OrderRepository> logger)
    {
        _logger = logger;
        var client = new TableServiceClient(StorageConfig.GetConnectionString(config));
        _table = client.GetTableClient(StorageConfig.GetOrdersTableName(config));
    }

    public async Task<OrderEntity> CreateReceivedAsync(OrderQueueMessage order, CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct);

        var entity = new OrderEntity
        {
            PartitionKey = order.OrderTimestamp.ToString("yyyy-MM-dd"),
            RowKey = order.OrderId,
            CustomerName = order.CustomerName,
            SelectedItemSKUs = string.Join(',', order.SelectedItemSKUs),
            TotalPrice = order.TotalPrice,
            Status = OrderStatus.Received,
            OrderTimestamp = order.OrderTimestamp,
            StatusUpdatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await _table.AddEntityAsync(entity, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new StorageConflictException($"An order with id '{order.OrderId}' already exists.");
        }

        _logger.LogInformation("Order {OrderId} recorded in Orders table (partition {Date})", entity.RowKey, entity.PartitionKey);
        return entity;
    }

    public async Task<OrderEntity?> AdvanceStatusAsync(string orderId, string newStatus, CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct);

        OrderEntity? entity = null;
        await foreach (var e in _table.QueryAsync<OrderEntity>(
            TableClient.CreateQueryFilter($"RowKey eq {orderId}"), cancellationToken: ct))
        {
            entity = e;
            break;
        }

        if (entity is null)
            return null;

        entity.Status = newStatus;
        entity.StatusUpdatedAt = DateTimeOffset.UtcNow;
        await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);

        _logger.LogInformation("Order {OrderId} transitioned to {Status}", orderId, newStatus);
        return entity;
    }

    public async Task<IReadOnlyList<OrderEntity>> GetAllAsync(CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct);
        var orders = new List<OrderEntity>();
        await foreach (var entity in _table.QueryAsync<OrderEntity>(cancellationToken: ct))
            orders.Add(entity);
        return orders;
    }

    public async Task<OrderEntity?> GetByIdAsync(string orderId, CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct);
        await foreach (var entity in _table.QueryAsync<OrderEntity>(
            TableClient.CreateQueryFilter($"RowKey eq {orderId}"), cancellationToken: ct))
            return entity;
        return null;
    }
}
