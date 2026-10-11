using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Services;

public interface IOrderQueueProducer
{
    Task<string> EnqueueAsync(QueueOrderRequest request, CancellationToken ct = default);
}
