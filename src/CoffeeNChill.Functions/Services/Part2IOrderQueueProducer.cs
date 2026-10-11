using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Services;

[Part2("Abstraction for enqueueing validated orders onto order-processing-queue")]
public interface IOrderQueueProducer
{
    Task<string> EnqueueAsync(QueueOrderRequest request, CancellationToken ct = default);
}
