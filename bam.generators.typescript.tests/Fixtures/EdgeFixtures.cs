using System.Text.Json.Serialization;

namespace Bam.Generators.TypeScript.Tests.Fixtures;

/// <summary>A base whose property a derived class hides.</summary>
public class HiddenBase
{
    /// <summary>Hidden in the derived class.</summary>
    public int Payload { get; init; }

    /// <summary>Not hidden.</summary>
    public long Sequence { get; init; }
}

/// <summary>Hides <see cref="HiddenBase.Payload"/> with a string.</summary>
public sealed class HidingDerived : HiddenBase
{
    /// <summary>The serializer writes this one.</summary>
    public new string Payload { get; init; } = string.Empty;
}

/// <summary>Two properties with one camelCase name.</summary>
public sealed class CaseCollision
{
    /// <summary>Written as "id".</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Also written as "id".</summary>
    public string ID { get; init; } = string.Empty;
}

/// <summary>Two properties renamed to one wire name.</summary>
public sealed class RenameCollision
{
    /// <summary>Renamed.</summary>
    [JsonPropertyName("value")]
    public string First { get; init; } = string.Empty;

    /// <summary>Renamed the same.</summary>
    [JsonPropertyName("value")]
    public string Second { get; init; } = string.Empty;
}

/// <summary>A custom list, which the serializer writes as an array.</summary>
public sealed class TagList : List<string>
{
}

/// <summary>A custom dictionary, which the serializer writes as an object of values.</summary>
public sealed class CountMap : Dictionary<string, int>
{
}

/// <summary>Holds a custom list.</summary>
/// <param name="Tags">The tags.</param>
public sealed record HasTagList(TagList Tags);

/// <summary>Holds a custom dictionary.</summary>
/// <param name="Counts">The counts.</param>
public sealed record HasCountMap(CountMap Counts);

/// <summary>Members the serializer can leave out.</summary>
public sealed class Sparse
{
    /// <summary>Left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>Left out when zero.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Count { get; init; }

    /// <summary>Always written.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string Name { get; init; } = string.Empty;
}

/// <summary>A flags enum.</summary>
[Flags]
public enum Perms
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Read.</summary>
    Read = 1,

    /// <summary>Write.</summary>
    Write = 2,
}

/// <summary>Holds a flags enum.</summary>
/// <param name="Granted">The permissions.</param>
public sealed record HasPerms(Perms Granted);

/// <summary>An enum with no members.</summary>
public enum Nothing
{
}

/// <summary>Holds the mappings beyond the issue's list.</summary>
public sealed record MoreShapes(
    DateOnly Day,
    TimeOnly At,
    TimeSpan Took,
    Uri Link,
    char Initial,
    IList<string> A,
    ICollection<string> B,
    IReadOnlyCollection<string> C,
    IEnumerable<string> D,
    IDictionary<string, int> E,
    Dictionary<string, int> F,
    IReadOnlyList<IReadOnlyList<int>> Grid,
    Outer.Inner Nested,
    Nothing Empty);

/// <summary>Declares a nested contract.</summary>
public static class Outer
{
    /// <summary>Generated as Outer_Inner.</summary>
    /// <param name="X">A value.</param>
    public sealed record Inner(int X);
}

/// <summary>Holds a multi-dimensional array.</summary>
/// <param name="Cells">The cells.</param>
public sealed record HasGrid(int[,] Cells);

/// <summary>An interface property type.</summary>
public interface IShape
{
}

/// <summary>Holds an interface.</summary>
/// <param name="Shape">The shape.</param>
public sealed record HasInterface(IShape Shape);

/// <summary>An abstract property type.</summary>
public abstract class AbstractShape
{
}

/// <summary>Holds an abstract type.</summary>
/// <param name="Shape">The shape.</param>
public sealed record HasAbstract(AbstractShape Shape);

/// <summary>Holds a framework generic.</summary>
/// <param name="Set">The set.</param>
public sealed record HasHashSet(HashSet<string> Set);

/// <summary>Would shadow TypeScript's Record.</summary>
/// <param name="X">A value.</param>
public sealed record Record(int X);

/// <summary>Holds the shadowing type.</summary>
/// <param name="Value">The value.</param>
public sealed record HasRecord(Record Value);

/// <summary>A typed id with an ignored helper property.</summary>
/// <param name="Value">The id.</param>
public readonly record struct TaggedId(string Value)
{
    /// <summary>Not on the wire.</summary>
    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrEmpty(Value);
}
