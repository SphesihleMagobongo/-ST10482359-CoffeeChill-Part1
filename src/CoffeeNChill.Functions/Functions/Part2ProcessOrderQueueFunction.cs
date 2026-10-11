using System.Text;
using System.Text.Json;
using Azure;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

[Part2("QueueTrigger consumer: parses payload, writes Orders table, advances lifecycle, logs poison failures")]
public class ProcessOrderQueueFunction
{
    // Statuses the worker walks an order through after recording it.
    private static readonly string[] Lifecycle =
        { OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Collected };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IOrderRepository _orders;
    private readonly int _statusDelayMs;
    private readonly ILogger<ProcessOrderQueueFunction> _logger;

    public ProcessOrderQueueFunction(IOrderRepository orders, IConfiguration config, ILogger<ProcessOrderQueueFunction> logger)
    {
        _orders = orders;
        _statusDelayMs = StorageConfig.GetOrderStatusDelayMs(config); // pause between status updates
        _logger = logger;
    }

    // Fires automatically whenever a message lands on order-processing-queue.
    [Function("ProcessOrderQueue")]
    public async Task Run(
        [QueueTrigger("order-processing-queue")] string message,
        CancellationToken ct)
    {
        OrderQueueMessage? order;
        try
        {
            var json = TryDecodeBase64(message); // producer sends Base64-encoded JSON
            order = JsonSerializer.Deserialize<OrderQueueMessage>(json, SerializerOptions);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            // Bad payload: log it and rethrow so the runtime retries, then moves it to the poison queue.
            _logger.LogError(ex, "POISON: failed to decode/parse queue message. Raw payload: {Payload}", message);
            throw;
        }

        if (order is null || string.IsNullOrWhiteSpace(order.OrderId))
        {
            // Message parsed but is not a usable order - treat it as poison too.
            _logger.LogError("POISON: queue message did not contain a valid order payload. Raw payload: {Payload}", message);
            throw new InvalidOperationException("Queue message was not a valid order payload.");
        }

        _logger.LogInformation("Dequeued order {OrderId} for {Customer}", order.OrderId, order.CustomerName);

        try
        {
            await _orders.CreateReceivedAsync(order, ct); // initial insert with Status = Received

            // Simulate the barista lifecycle: Received -> Preparing -> Ready -> Collected.
            foreach (var status in Lifecycle)
            {
                await Task.Delay(_statusDelayMs, ct);
                await _orders.AdvanceStatusAsync(order.OrderId, status, ct);
            }
        }
        catch (RequestFailedException ex)
        {
            // Storage hiccup: rethrow so the runtime retries the message (poisoned after max retries).
            _logger.LogError(ex, "Storage failure while processing order {OrderId} - message will be retried and eventually poisoned", order.OrderId);
            throw;
        }

        _logger.LogInformation("Order {OrderId} lifecycle complete (Received -> Preparing -> Ready -> Collected)", order.OrderId);
    }

    // Decodes Base64 when the producer encoded it; otherwise returns the raw text.
    private static string TryDecodeBase64(string input)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(input));
        }
        catch (FormatException)
        {
            return input; // message was already plain JSON
        }
    }
}
