namespace Rulesage.Composition.Types;

public record Grammar(string Definition, string Format = "JSON_SCHEMA")
{
    public static Grammar Empty => new("{}");
}