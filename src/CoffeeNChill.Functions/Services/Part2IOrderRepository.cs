using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Services;

[Part2("Abstraction for the Orders Azure Table (create, status transitions, reads)")]
public interface IOrderRepository
{
    Task<OrderEntity> CreateReceivedAsync(OrderQueueMessage order, CancellationToken ct = default);          // insert with Status = Received
    Task<OrderEntity?> AdvanceStatusAsync(string orderId, string newStatus, CancellationToken ct = default); // move to next lifecycle status
    Task<IReadOnlyList<OrderEntity>> GetAllAsync(CancellationToken ct = default);                            // all orders
    Task<OrderEntity?> GetByIdAsync(string orderId, CancellationToken ct = default);                         // single order by RowKey
}
