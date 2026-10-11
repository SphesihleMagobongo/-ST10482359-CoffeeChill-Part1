using System.Text;
using System.Text.Json;
using Azure.Storage.Queues;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Services;

[Part2("Serializes orders to JSON, Base64-encodes and pushes to order-processing-queue; generates OrderId + timestamp")]
public class OrderQueueProducer : IOrderQueueProducer
{
    private static readonly JsonSerializerOptions SerializerOptions = new();

    private readonly QueueClient _queue;
    private readonly ILogger<OrderQueueProducer> _logger;

    public OrderQueueProducer(IConfiguration config, ILogger<OrderQueueProducer> logger)
    {
        _logger = logger;
        _queue = new QueueClient(
            StorageConfig.GetConnectionString(config),
            StorageConfig.GetOrderQueueName(config));
    }

    public async Task<string> EnqueueAsync(QueueOrderRequest request, CancellationToken ct = default)
    {
        // Build the queue message; server generates OrderId + timestamp so clients can't spoof them.
        var message = new OrderQueueMessage
        {
            OrderId = string.IsNullOrWhiteSpace(request.OrderId)
                ? $"ORD-{DateTimeOffset.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}"
                : request.OrderId.Trim(),
            CustomerName = request.CustomerName.Trim(),
            SelectedItemSKUs = request.SelectedItemSKUs.Select(s => s.Trim()).ToList(),
            TotalPrice = request.TotalPrice,
            OrderTimestamp = DateTimeOffset.UtcNow
        };

        // Serialize to JSON, then Base64-encode before pushing onto the queue.
        var json = JsonSerializer.Serialize(message, SerializerOptions);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        await _queue.CreateIfNotExistsAsync(cancellationToken: ct); // ensure the queue exists
        await _queue.SendMessageAsync(base64, cancellationToken: ct);

        _logger.LogInformation("Queued order {OrderId} for {Customer} ({ItemCount} items, R{Total})",
            message.OrderId, message.CustomerName, message.SelectedItemSKUs.Count, message.TotalPrice);

        return message.OrderId;
    }
}
