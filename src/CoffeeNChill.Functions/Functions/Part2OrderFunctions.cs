using System.Net;
using Azure;
using CoffeeNChill.Functions.Helpers;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

[Part2("HTTP endpoints: POST /api/orders/queue producer + GET /api/orders status readers")]
public class OrderFunctions
{
    private readonly IOrderQueueProducer _queue;
    private readonly IOrderRepository _orders;
    private readonly ILogger<OrderFunctions> _logger;

    public OrderFunctions(IOrderQueueProducer queue, IOrderRepository orders, ILogger<OrderFunctions> logger)
    {
        _queue = queue;
        _orders = orders;
        _logger = logger;
    }

    // Queue producer: validates the payload, pushes it to order-processing-queue, returns 202.
    // The client gets an instant response while a queue-triggered function processes it in the background.
    [Function("QueueOrder")]
    public async Task<HttpResponseData> QueueOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/queue")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await HttpHelpers.ReadJsonAsync<QueueOrderRequest>(req);
        if (body is null)
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest, "Request body must be valid JSON.");

        if (!HttpHelpers.TryValidate(body, out var errors))
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.BadRequest, "Validation failed.", errors);

        try
        {
            var orderId = await _queue.EnqueueAsync(body, ct);
            return await HttpHelpers.JsonAsync(req, HttpStatusCode.Accepted, new
            {
                orderId,
                status = "Queued",
                queue = "order-processing-queue"
            });
        }
        catch (StorageConflictException ex)
        {
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.Conflict, ex.Message);
        }
        catch (Exception ex) when (ex is RequestFailedException or AggregateException or TaskCanceledException)
        {
            // Queue unreachable (Azurite down etc.) - report 503 rather than a raw 500.
            _logger.LogError(ex, "Queue storage operation failed");
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.ServiceUnavailable,
                "Order queue is currently unavailable. Please retry.");
        }
    }

    // Returns every order in the Orders table with its current lifecycle status.
    [Function("GetAllOrders")]
    public async Task<HttpResponseData> GetAllOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req,
        CancellationToken ct)
    {
        var orders = await _orders.GetAllAsync(ct);
        return await HttpHelpers.JsonAsync(req, HttpStatusCode.OK, orders.Select(o => o.ToResponse()).ToList());
    }

    // Returns a single order by OrderId (404 when it does not exist).
    [Function("GetOrderById")]
    public async Task<HttpResponseData> GetOrderById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{id}")] HttpRequestData req,
        string id,
        CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(Uri.UnescapeDataString(id), ct);
        if (order is null)
            return await HttpHelpers.ErrorAsync(req, HttpStatusCode.NotFound, $"Order '{id}' was not found.");

        return await HttpHelpers.JsonAsync(req, HttpStatusCode.OK, order.ToResponse());
    }
}
