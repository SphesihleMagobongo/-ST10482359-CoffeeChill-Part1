using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Functions.Models;

// What the client POSTs to /api/orders/queue. OrderId and OrderTimestamp are server-generated.
[Part2("Client payload for POST /api/orders/queue with DataAnnotations validation")]
public class QueueOrderRequest
{
    [RegularExpression(@"^[A-Za-z0-9_-]{2,50}$", ErrorMessage = "orderId may only contain letters, digits, hyphens and underscores (2-50 chars)")]
    public string? OrderId { get; set; } // optional - generated when omitted

    [Required(ErrorMessage = "customerName is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "customerName must be between 2 and 100 characters")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "selectedItemSKUs is required")]
    [MinLength(1, ErrorMessage = "selectedItemSKUs must contain at least one item")]
    public List<string> SelectedItemSKUs { get; set; } = new();

    [Range(0.01, 100000, ErrorMessage = "totalPrice must be between 0.01 and 100000")]
    public double TotalPrice { get; set; }
}

// The JSON contract that is Base64-encoded onto order-processing-queue.
[Part2("Message contract serialized to JSON and Base64-encoded onto order-processing-queue")]
public class OrderQueueMessage
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<string> SelectedItemSKUs { get; set; } = new();
    public double TotalPrice { get; set; }
    public DateTimeOffset OrderTimestamp { get; set; }
}

// Shape returned by the GET /api/orders endpoints.
public record OrderResponse(
    string OrderId,
    string OrderDate,
    string CustomerName,
    List<string> SelectedItemSKUs,
    double TotalPrice,
    string Status,
    DateTimeOffset OrderTimestamp,
    DateTimeOffset? StatusUpdatedAt);

public static class OrderMapping
{
    // Maps a table entity to the API response (splits the comma-separated SKUs back into a list).
    public static OrderResponse ToResponse(this OrderEntity e) =>
        new(e.RowKey,
            e.PartitionKey,
            e.CustomerName,
            e.SelectedItemSKUs.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            e.TotalPrice,
            e.Status,
            e.OrderTimestamp,
            e.StatusUpdatedAt);
}
