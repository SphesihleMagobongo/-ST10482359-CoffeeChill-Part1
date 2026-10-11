using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.Functions.Models;

public static class OrderStatus
{
    public const string Received = "Received";
    public const string Preparing = "Preparing";
    public const string Ready = "Ready";
    public const string Collected = "Collected";
}

public class OrderEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string SelectedItemSKUs { get; set; } = string.Empty;
    public double TotalPrice { get; set; }
    public string Status { get; set; } = OrderStatus.Received;
    public DateTimeOffset OrderTimestamp { get; set; }
    public DateTimeOffset? StatusUpdatedAt { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
