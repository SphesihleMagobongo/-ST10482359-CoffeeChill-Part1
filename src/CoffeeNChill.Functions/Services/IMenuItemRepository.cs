using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Services;

public interface IMenuItemRepository
{
    Task<MenuItemEntity> CreateAsync(MenuItemEntity entity, CancellationToken ct = default);
    Task<IReadOnlyList<MenuItemEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MenuItemEntity>> GetByCategoryAsync(string category, CancellationToken ct = default);
    Task<MenuItemEntity?> GetAsync(string category, string id, CancellationToken ct = default);
    Task<MenuItemEntity?> UpdateAsync(string category, string id, double? price, bool? isAvailable, CancellationToken ct = default);
    Task<bool> DeleteAsync(string category, string id, CancellationToken ct = default);
}
