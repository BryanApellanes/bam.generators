using System.Text.Json.Serialization;

namespace Bam.Generators.TypeScript.Tests.Fixtures;

/// <summary>A typed id: a record struct wrapping one string.</summary>
/// <param name="Value">The id.</param>
public readonly record struct NodeId(string Value);

/// <summary>An enum, written as a string union.</summary>
public enum NodeHealth
{
    /// <summary>Healthy.</summary>
    Healthy,

    /// <summary>Degraded.</summary>
    Degraded,

    /// <summary>Renamed on the wire.</summary>
    [JsonStringEnumMemberName("not-reporting")]
    NotReporting,
}

/// <summary>A message with every mapped shape.</summary>
public sealed record NodeSnapshot(
    NodeId Id,
    string Name,
    string? Description,
    int Port,
    long Count,
    double Ratio,
    decimal Price,
    bool Active,
    int? Retries,
    DateTimeOffset ObservedAt,
    DateTime UpdatedAt,
    Guid Instance,
    NodeHealth Health,
    NodeHealth? PreviousHealth,
    IReadOnlyList<string> Tags,
    List<NodeEdge> Edges,
    string[] Aliases,
    IReadOnlyList<string?> Notes,
    IReadOnlyDictionary<string, double> Metrics,
    IReadOnlyDictionary<string, NodeEdge?> Peers,
    NodeEdge? Parent,
    byte[] Thumbprint)
{
    /// <summary>Not on the wire.</summary>
    [JsonIgnore]
    public string Ignored => "x";

    /// <summary>Renamed on the wire.</summary>
    [JsonPropertyName("x-trace")]
    public string Trace { get; init; } = string.Empty;
}

/// <summary>A type only reached through a property.</summary>
/// <param name="From">The source node.</param>
/// <param name="To">The target node.</param>
public sealed record NodeEdge(NodeId From, NodeId To);

/// <summary>A base class, to check member order.</summary>
public class BaseMessage
{
    /// <summary>The sequence number.</summary>
    public long Sequence { get; init; }
}

/// <summary>A derived message.</summary>
public sealed class DerivedMessage : BaseMessage
{
    /// <summary>The payload.</summary>
    public string Payload { get; init; } = string.Empty;
}

/// <summary>Holds something the target can't express.</summary>
/// <param name="Anything">An object.</param>
public sealed record Untyped(object Anything);

/// <summary>Holds a dictionary keyed by int.</summary>
/// <param name="ByNumber">The map.</param>
public sealed record IntKeyed(IReadOnlyDictionary<int, string> ByNumber);

/// <summary>A generic contract.</summary>
/// <typeparam name="T">The item.</typeparam>
/// <param name="Items">The items.</param>
public sealed record Page<T>(IReadOnlyList<T> Items);

/// <summary>Holds a closed generic contract.</summary>
/// <param name="Edges">A page.</param>
public sealed record EdgePage(Page<NodeEdge> Edges);

/// <summary>Self-referencing, to check the walk terminates.</summary>
/// <param name="Children">The children.</param>
public sealed record TreeNode(IReadOnlyList<TreeNode> Children);

/// <summary>Holds a second type named NodeEdge.</summary>
/// <param name="Edge">The other edge.</param>
public sealed record Clash(Other.NodeEdge Edge);
