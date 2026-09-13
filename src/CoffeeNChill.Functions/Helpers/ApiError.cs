namespace CoffeeNChill.Functions.Helpers;

public record ApiError(int Status, string Message, IReadOnlyList<string>? Errors = null);
