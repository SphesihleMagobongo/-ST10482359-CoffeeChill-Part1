using Microsoft.Extensions.Configuration;

namespace CoffeeNChill.Functions.Services;

public static class StorageConfig
{
    public static string GetConnectionString(IConfiguration config) =>
        config["AzureWebJobsStorage"]
        ?? config["Storage:ConnectionString"]
        ?? "UseDevelopmentStorage=true";

    public static string GetMenuTableName(IConfiguration config) =>
        config["Storage:MenuTableName"] ?? "MenuItems";

    public static string GetStaffDocsShareName(IConfiguration config) =>
        config["Storage:StaffDocsShareName"] ?? "staff-docs";

    public static long GetMaxUploadBytes(IConfiguration config) =>
        long.TryParse(config["Storage:MaxUploadBytes"], out var v) ? v : 10 * 1024 * 1024;
}

public class StorageConflictException : Exception
{
    public StorageConflictException(string message) : base(message) { }
}
