namespace CoffeeNChill.Functions;

/// <summary>
/// Marks classes introduced for Part 2 (Queue Triggers &amp; Docker Compose Orchestration).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class Part2Attribute : Attribute
{
    public string Description { get; }

    public Part2Attribute(string description) => Description = description;
}
