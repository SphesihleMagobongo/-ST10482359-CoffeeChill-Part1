using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Functions.Models;

public class CreateMenuItemRequest
{
    [Required(ErrorMessage = "category is required")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "category must be between 2 and 50 characters")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "sku is required")]
    [RegularExpression(@"^[A-Za-z0-9_-]{2,50}$", ErrorMessage = "sku may only contain letters, digits, hyphens and underscores (2-50 chars)")]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "name must be between 2 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "description cannot exceed 500 characters")]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 100000, ErrorMessage = "price must be between 0.01 and 100000")]
    public double Price { get; set; }

    public bool IsAvailable { get; set; } = true;
}

public class UpdateMenuItemRequest
{
    [Range(0.01, 100000, ErrorMessage = "price must be between 0.01 and 100000")]
    public double? Price { get; set; }

    public bool? IsAvailable { get; set; }

    public bool HasChanges => Price.HasValue || IsAvailable.HasValue;
}

public record MenuItemResponse(
    string Category,
    string Id,
    string Name,
    string Description,
    double Price,
    bool IsAvailable,
    DateTimeOffset? LastModified);

public static class MenuItemMapping
{
    public static MenuItemResponse ToResponse(this MenuItemEntity e) =>
        new(e.PartitionKey, e.RowKey, e.Name, e.Description, e.Price, e.IsAvailable, e.Timestamp);
}
