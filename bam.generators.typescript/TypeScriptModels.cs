namespace Bam.Generators.TypeScript;

/// <summary>How generated names are spelled.</summary>
public enum TypeScriptNaming
{
    /// <summary>As declared in C#.</summary>
    AsDeclared,

    /// <summary>camelCase, as <c>System.Text.Json</c>'s <c>JsonNamingPolicy.CamelCase</c> writes it (ASP.NET Core's default for properties).</summary>
    CamelCase,
}

/// <summary>What a generated TypeScript declaration is.</summary>
public enum TypeScriptDeclarationKind
{
    /// <summary>An <c>export interface</c> from a C# record, class or struct.</summary>
    Interface,

    /// <summary>A string-literal union from a C# enum.</summary>
    StringUnion,

    /// <summary>A branded <c>string</c> from a struct wrapping a single string (a typed id).</summary>
    BrandedString,
}

/// <summary>Options for <see cref="TypeScriptContractGenerator"/>.</summary>
public sealed class TypeScriptGenerationOptions
{
    /// <summary>Gets how property names are written; defaults to <see cref="TypeScriptNaming.CamelCase"/>. A <c>[JsonPropertyName]</c> always wins.</summary>
    public TypeScriptNaming PropertyNaming { get; init; } = TypeScriptNaming.CamelCase;

    /// <summary>
    /// Gets how enum members are written; defaults to <see cref="TypeScriptNaming.AsDeclared"/>, which is what
    /// <c>JsonStringEnumConverter</c> writes without a naming policy. A <c>[JsonStringEnumMemberName]</c> always wins.
    /// </summary>
    public TypeScriptNaming EnumMemberNaming { get; init; } = TypeScriptNaming.AsDeclared;
}

/// <summary>One property of a generated interface.</summary>
/// <param name="Name">The property name as written in TypeScript (quoted when it isn't an identifier).</param>
/// <param name="Type">The TypeScript type.</param>
/// <param name="Optional">Whether the serializer can leave the key out (a <c>[JsonIgnore]</c> condition other than <c>Always</c>).</param>
public sealed record TypeScriptMemberModel(string Name, string Type, bool Optional = false);

/// <summary>One generated declaration.</summary>
public sealed class TypeScriptDeclarationModel
{
    /// <summary>Initializes a declaration.</summary>
    /// <param name="name">The TypeScript name.</param>
    /// <param name="kind">What it is.</param>
    /// <param name="sourceType">The C# type it comes from.</param>
    /// <param name="members">The interface members, in order; empty for other kinds.</param>
    /// <param name="literals">The union's string literals, already quoted, in order; empty for other kinds.</param>
    public TypeScriptDeclarationModel(string name, TypeScriptDeclarationKind kind, Type sourceType, IReadOnlyList<TypeScriptMemberModel> members, IReadOnlyList<string> literals)
    {
        Name = name;
        Kind = kind;
        SourceType = sourceType;
        Members = members;
        Literals = literals;
    }

    /// <summary>Gets the TypeScript name.</summary>
    public string Name { get; }

    /// <summary>Gets what the declaration is.</summary>
    public TypeScriptDeclarationKind Kind { get; }

    /// <summary>Gets the C# type it comes from.</summary>
    public Type SourceType { get; }

    /// <summary>Gets the C# type's full name, for the comment above the declaration.</summary>
    public string SourceTypeName => new string((SourceType.FullName ?? SourceType.Name).Select(character => char.IsControl(character) || character == '\u2028' || character == '\u2029' ? ' ' : character).ToArray());

    /// <summary>Gets the interface members, in order.</summary>
    public IReadOnlyList<TypeScriptMemberModel> Members { get; }

    /// <summary>Gets the union literals, quoted, in order.</summary>
    public IReadOnlyList<string> Literals { get; }

    /// <summary>Gets the union's right-hand side (<c>"A" | "B"</c>), or <c>never</c> for an enum with no members.</summary>
    public string Union => Literals.Count == 0 ? "never" : string.Join(" | ", Literals);

    /// <summary>Gets the brand literal for a typed id.</summary>
    public string Brand => System.Text.Json.JsonSerializer.Serialize(Name);

    /// <summary>Gets whether this is an interface (for the template).</summary>
    public bool IsInterface => Kind == TypeScriptDeclarationKind.Interface;

    /// <summary>Gets whether this is a string union (for the template).</summary>
    public bool IsStringUnion => Kind == TypeScriptDeclarationKind.StringUnion;

    /// <summary>Gets whether this is a branded string (for the template).</summary>
    public bool IsBrandedString => Kind == TypeScriptDeclarationKind.BrandedString;
}

/// <summary>Everything one generated module declares, sorted by name.</summary>
/// <param name="Declarations">The declarations, in ordinal order of their names.</param>
public sealed record TypeScriptContractModel(IReadOnlyList<TypeScriptDeclarationModel> Declarations);

/// <summary>A C# type the TypeScript target can't express; the message names the type and where it was reached from.</summary>
public sealed class TypeScriptGenerationException : Exception
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="message">What can't be generated, and why.</param>
    public TypeScriptGenerationException(string message) : base(message)
    {
    }
}
