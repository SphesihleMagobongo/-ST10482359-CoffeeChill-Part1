using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Services;

[Part2("Abstraction for enqueueing validated orders onto order-processing-queue")]
public interface IOrderQueueProducer
{
    // Pushes a validated order onto order-processing-queue; returns the OrderId.
    Task<string> EnqueueAsync(QueueOrderRequest request, CancellationToken ct = default);
}
