using System.Net;
using CoffeeNChill.Functions.Helpers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace CoffeeNChill.Functions.Functions;

public class HealthFunction
{
    private readonly IConfiguration _config;

    public HealthFunction(IConfiguration config) => _config = config;

    public record HealthStatus(string Status, string StorageMode, DateTimeOffset UtcNow);

    [Function("HealthCheck")]
    public async Task<HttpResponseData> HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var payload = new HealthStatus("Healthy", _config["Storage:Mode"] ?? "Azurite", DateTimeOffset.UtcNow);
        return await HttpHelpers.JsonAsync(req, HttpStatusCode.OK, payload);
    }
}
