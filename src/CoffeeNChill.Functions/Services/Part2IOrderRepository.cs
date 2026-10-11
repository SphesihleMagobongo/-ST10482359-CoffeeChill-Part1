using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Services;

[Part2("Abstraction for the Orders Azure Table (create, status transitions, reads)")]
public interface IOrderRepository
{
    Task<OrderEntity> CreateReceivedAsync(OrderQueueMessage order, CancellationToken ct = default);
    Task<OrderEntity?> AdvanceStatusAsync(string orderId, string newStatus, CancellationToken ct = default);
    Task<IReadOnlyList<OrderEntity>> GetAllAsync(CancellationToken ct = default);
    Task<OrderEntity?> GetByIdAsync(string orderId, CancellationToken ct = default);
}
