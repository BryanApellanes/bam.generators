using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bam.Generators.TypeScript.Tests.Fixtures;

/// <summary>A generic base whose members a closed contract inherits.</summary>
/// <typeparam name="T">The value type.</typeparam>
public class GenericBase<T>
{
    /// <summary>The value.</summary>
    public T Value { get; init; } = default!;

    /// <summary>The items.</summary>
    public T[] Items { get; init; } = Array.Empty<T>();
}

/// <summary>Closes the base over a non-nullable string.</summary>
public sealed class StringsOnGeneric : GenericBase<string>
{
}

/// <summary>Closes the base over a list that can hold nulls.</summary>
public sealed class ListsOnGeneric : GenericBase<List<string?>>
{
}

/// <summary>Writes a DateTime as a number.</summary>
public sealed class EpochConverter : JsonConverter<DateTime>
{
    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => DateTime.UnixEpoch.AddSeconds(reader.GetInt64());

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteNumberValue((long)(value - DateTime.UnixEpoch).TotalSeconds);
}

/// <summary>A property with its own converter.</summary>
/// <param name="At">Written as a number.</param>
public sealed record WithConverter([property: JsonConverter(typeof(EpochConverter))] DateTime At);

/// <summary>A property whose number is written as a string.</summary>
/// <param name="Big">Written as a string.</param>
public sealed record WithNumbersAsStrings([property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] long Big);

/// <summary>An enum property with a string-enum converter, which is still a union.</summary>
/// <param name="Health">The health.</param>
public sealed record WithEnumConverter([property: JsonConverter(typeof(JsonStringEnumConverter<NodeHealth>))] NodeHealth Health);

/// <summary>Writes numbers as strings for every member of the class.</summary>
[JsonNumberHandling(JsonNumberHandling.WriteAsString)]
public sealed class ClassNumbersAsStrings
{
    /// <summary>Written as a string.</summary>
    public long Big { get; init; }
}

/// <summary>Writes a typed id as its plain string value.</summary>
public sealed class NodeIdConverter : JsonConverter<NodeId>
{
    /// <inheritdoc />
    public override NodeId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new NodeId(reader.GetString() ?? string.Empty);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, NodeId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
}

/// <summary>A typed-id property with its own string converter, which is still the brand.</summary>
/// <param name="Id">The id.</param>
public sealed record WithTypedIdConverter([property: JsonConverter(typeof(NodeIdConverter))] NodeId Id);

/// <summary>Included fields.</summary>
public sealed class WithFields
{
    /// <summary>Written.</summary>
    public string Z { get; init; } = string.Empty;

    /// <summary>Included and nullable.</summary>
    [JsonInclude]
    public string? MaybeShown;

    /// <summary>Included and renamed.</summary>
    [JsonInclude]
    [JsonPropertyName("renamed-field")]
    public string Field = string.Empty;

    /// <summary>Not included, so not written.</summary>
    public string Hidden = string.Empty;
}
