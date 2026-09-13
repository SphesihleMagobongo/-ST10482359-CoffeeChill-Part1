using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace CoffeeNChill.Functions.Helpers;

public static class HttpHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<T?> ReadJsonAsync<T>(HttpRequestData req) where T : class
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(req.Body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool TryValidate<T>(T model, out List<string> errors) where T : class
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        errors = results.Select(r => r.ErrorMessage ?? "Invalid value").ToList();
        return isValid;
    }

    public static async Task<HttpResponseData> JsonAsync<T>(HttpRequestData req, HttpStatusCode status, T payload)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await JsonSerializer.SerializeAsync(response.Body, payload, JsonOptions);
        return response;
    }

    public static Task<HttpResponseData> ErrorAsync(HttpRequestData req, HttpStatusCode status, string message, IEnumerable<string>? errors = null) =>
        JsonAsync(req, status, new ApiError((int)status, message, errors?.ToList()));
}
