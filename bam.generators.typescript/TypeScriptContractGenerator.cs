using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bam.Generators.TypeScript;

/// <summary>
/// Generates TypeScript declarations from C# contract types (bam.generators#37), so REST and SignalR DTOs are defined
/// once, in C#, and a consumer's CI can fail when the checked-in TypeScript drifts.
/// <list type="bullet">
/// <item><description>a record, class or struct becomes an <c>export interface</c> of <c>readonly</c> properties;</description></item>
/// <item><description>an enum becomes a string-literal union;</description></item>
/// <item><description>a struct wrapping a single <c>string</c> (a typed id) becomes a branded <c>string</c>;</description></item>
/// <item><description><c>T[]</c>, <c>List&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c> and the other sequence interfaces become <c>readonly T[]</c>;</description></item>
/// <item><description>a <c>string</c>-keyed dictionary becomes <c>Readonly&lt;Record&lt;string, T&gt;&gt;</c>;</description></item>
/// <item><description><c>Nullable&lt;T&gt;</c> and a nullable reference become <c>T | null</c>;</description></item>
/// <item><description>dates, times, <c>Guid</c>, <c>Uri</c>, <c>char</c> and <c>byte[]</c> (base64) become <c>string</c>; numbers become <c>number</c>; <c>bool</c> becomes <c>boolean</c>.</description></item>
/// </list>
/// Types a property refers to are generated too, so passing the root messages is enough. Output is deterministic:
/// declarations are sorted by name (ordinal), members keep declaration order, lines end in <c>\n</c>, and there is no
/// timestamp. Anything the target can't express (<c>object</c>, an open or generic contract type, a dictionary keyed by
/// something other than <c>string</c>, two types with one TypeScript name) throws a <see cref="TypeScriptGenerationException"/>
/// naming it, rather than emitting <c>any</c>.
/// </summary>
public sealed class TypeScriptContractGenerator
{
    private static readonly HashSet<Type> StringTypes = new HashSet<Type>
    {
        typeof(string), typeof(char), typeof(Guid), typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly),
        typeof(TimeOnly), typeof(TimeSpan), typeof(Uri), typeof(byte[]),
    };

    private static readonly HashSet<Type> NumberTypes = new HashSet<Type>
    {
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long),
        typeof(ulong), typeof(float), typeof(double), typeof(decimal),
    };

    private static readonly HashSet<Type> SequenceDefinitions = new HashSet<Type>
    {
        typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>), typeof(IEnumerable<>), typeof(ICollection<>),
        typeof(IList<>), typeof(List<>),
    };

    private static readonly HashSet<Type> DictionaryDefinitions = new HashSet<Type>
    {
        typeof(IReadOnlyDictionary<,>), typeof(IDictionary<,>), typeof(Dictionary<,>),
    };

    private readonly TypeScriptGenerationOptions _options;
    private readonly ITypeScriptCodeWriter _writer;

    /// <summary>Initializes a generator with default options and the embedded template.</summary>
    public TypeScriptContractGenerator() : this(new TypeScriptGenerationOptions(), new HandlebarsTypeScriptCodeWriter())
    {
    }

    /// <summary>Initializes a generator.</summary>
    /// <param name="options">Naming options.</param>
    /// <param name="writer">Renders the model.</param>
    public TypeScriptContractGenerator(TypeScriptGenerationOptions options, ITypeScriptCodeWriter writer)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>Builds the model for <paramref name="roots"/> and every contract type they reach.</summary>
    /// <param name="roots">The contract types to start from.</param>
    /// <returns>The declarations, sorted by name.</returns>
    /// <exception cref="TypeScriptGenerationException">A type can't be expressed, or two types share a TypeScript name.</exception>
    public TypeScriptContractModel BuildModel(IEnumerable<Type> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        Walk walk = new Walk(this);
        foreach (Type root in roots.OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            walk.Declare(root, $"the root {root.FullName}");
        }
        return new TypeScriptContractModel(walk.Declarations.Values.OrderBy(declaration => declaration.Name, StringComparer.Ordinal).ToList());
    }

    /// <summary>Builds the model for the public types of <paramref name="assembly"/> that <paramref name="include"/> selects.</summary>
    /// <param name="assembly">The contracts assembly.</param>
    /// <param name="include">Selects the root types, for example by namespace; null takes every public class, struct and enum.</param>
    /// <returns>The declarations, sorted by name.</returns>
    public TypeScriptContractModel BuildModel(Assembly assembly, Func<Type, bool>? include = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        IEnumerable<Type> roots = assembly.GetExportedTypes()
            .Where(type => (type.IsClass || type.IsValueType) && !type.IsAbstract && !type.IsGenericTypeDefinition && !typeof(Delegate).IsAssignableFrom(type) && !IsAttribute(type))
            .Where(type => include is null || include(type));
        return BuildModel(roots);
    }

    /// <summary>Generates the TypeScript module for <paramref name="roots"/>.</summary>
    /// <param name="roots">The contract types to start from.</param>
    /// <returns>The module source, with <c>\n</c> line endings.</returns>
    public string GetSource(IEnumerable<Type> roots) => _writer.GetSource(BuildModel(roots));

    /// <summary>Generates the TypeScript module for the selected public types of <paramref name="assembly"/>.</summary>
    /// <param name="assembly">The contracts assembly.</param>
    /// <param name="include">Selects the root types; null takes them all.</param>
    /// <returns>The module source, with <c>\n</c> line endings.</returns>
    public string GetSource(Assembly assembly, Func<Type, bool>? include = null) => _writer.GetSource(BuildModel(assembly, include));

    /// <summary>
    /// Writes the module for <paramref name="roots"/> to <paramref name="path"/> as UTF-8 without a byte-order mark,
    /// creating the directory. This is the call a console tool makes; the same input writes the same bytes.
    /// </summary>
    /// <param name="path">The <c>.ts</c> file to write.</param>
    /// <param name="roots">The contract types to start from.</param>
    public void WriteFile(string path, IEnumerable<Type> roots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string source = GetSource(roots);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, source, new UTF8Encoding(false));
    }

    private static bool IsAttribute(Type type) => typeof(Attribute).IsAssignableFrom(type);

    private static string ConvertName(string name, TypeScriptNaming naming) => naming == TypeScriptNaming.CamelCase ? JsonNamingPolicy.CamelCase.ConvertName(name) : name;

    private static string Quote(string value) => JsonSerializer.Serialize(value);

    private static string PropertyKey(string name)
    {
        bool identifier = name.Length > 0
            && (char.IsLetter(name[0]) || name[0] == '_' || name[0] == '$')
            && name.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '$');
        return identifier ? name : Quote(name);
    }

    private static string TypeScriptName(Type type)
    {
        string name = type.Name;
        return type.DeclaringType is null ? name : TypeScriptName(type.DeclaringType) + "_" + name;
    }

    private static bool IsTypedId(Type type)
    {
        if (!type.IsValueType || type.IsPrimitive || type.IsEnum || type.IsGenericType || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
        {
            return false;
        }
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.GetIndexParameters().Length == 0).ToArray();
        return properties.Length == 1 && properties[0].PropertyType == typeof(string);
    }

    /// <summary>One run of the walk: the declarations found so far, keyed by C# type.</summary>
    private sealed class Walk
    {
        private readonly TypeScriptContractGenerator _generator;
        private readonly NullabilityInfoContext _nullability = new NullabilityInfoContext();
        private readonly Dictionary<string, Type> _names = new Dictionary<string, Type>(StringComparer.Ordinal);
        private readonly HashSet<Type> _inProgress = new HashSet<Type>();

        public Walk(TypeScriptContractGenerator generator)
        {
            _generator = generator;
        }

        public Dictionary<Type, TypeScriptDeclarationModel> Declarations { get; } = new Dictionary<Type, TypeScriptDeclarationModel>();

        public string Declare(Type type, string reachedFrom)
        {
            if (Declarations.TryGetValue(type, out TypeScriptDeclarationModel? existing))
            {
                return existing.Name;
            }
            string name = TypeScriptName(type);
            if (_inProgress.Contains(type))
            {
                return name;
            }
            if (type.IsGenericType || type.ContainsGenericParameters)
            {
                throw new TypeScriptGenerationException($"{type.FullName ?? type.Name}, reached from {reachedFrom}, is generic; the TypeScript target generates only non-generic contract types. Declare a closed record for it.");
            }
            if (type == typeof(object) || type.IsInterface || type.IsAbstract || type.IsPointer || typeof(Delegate).IsAssignableFrom(type) || type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
            {
                throw new TypeScriptGenerationException($"{type.FullName ?? type.Name}, reached from {reachedFrom}, has no TypeScript mapping. Use a concrete contract record, or one of the mapped types.");
            }
            if (_names.TryGetValue(name, out Type? other) && other != type)
            {
                throw new TypeScriptGenerationException($"{type.FullName} and {other.FullName} would both be generated as {name}. Rename one.");
            }
            _names[name] = type;
            _inProgress.Add(type);
            TypeScriptDeclarationModel declaration;
            if (type.IsEnum)
            {
                declaration = new TypeScriptDeclarationModel(name, TypeScriptDeclarationKind.StringUnion, type, Array.Empty<TypeScriptMemberModel>(), EnumLiterals(type));
            }
            else if (IsTypedId(type))
            {
                declaration = new TypeScriptDeclarationModel(name, TypeScriptDeclarationKind.BrandedString, type, Array.Empty<TypeScriptMemberModel>(), Array.Empty<string>());
            }
            else
            {
                declaration = new TypeScriptDeclarationModel(name, TypeScriptDeclarationKind.Interface, type, Members(type), Array.Empty<string>());
            }
            _inProgress.Remove(type);
            Declarations[type] = declaration;
            return name;
        }

        private IReadOnlyList<string> EnumLiterals(Type type)
        {
            List<string> literals = new List<string>();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(field => field.MetadataToken))
            {
                JsonStringEnumMemberNameAttribute? renamed = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();
                string value = renamed?.Name ?? ConvertName(field.Name, _generator._options.EnumMemberNaming);
                literals.Add(Quote(value));
            }
            return literals;
        }

        private IReadOnlyList<TypeScriptMemberModel> Members(Type type)
        {
            List<TypeScriptMemberModel> members = new List<TypeScriptMemberModel>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<PropertyInfo> properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod is not null && property.GetMethod.IsPublic)
                .OrderBy(property => Depth(property.DeclaringType!))
                .ThenBy(property => property.MetadataToken);
            foreach (PropertyInfo property in properties)
            {
                JsonIgnoreAttribute? ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
                if (ignore is not null && ignore.Condition == JsonIgnoreCondition.Always)
                {
                    continue;
                }
                JsonPropertyNameAttribute? renamed = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                string name = renamed?.Name ?? ConvertName(property.Name, _generator._options.PropertyNaming);
                if (!seen.Add(name))
                {
                    continue;
                }
                string reachedFrom = $"{type.FullName}.{property.Name}";
                members.Add(new TypeScriptMemberModel(PropertyKey(name), Map(property.PropertyType, _nullability.Create(property), reachedFrom)));
            }
            return members;
        }

        private static int Depth(Type type)
        {
            int depth = 0;
            for (Type? current = type.BaseType; current is not null; current = current.BaseType)
            {
                depth++;
            }
            return depth;
        }

        private string Map(Type type, NullabilityInfo? nullability, string reachedFrom)
        {
            Type? underlying = Nullable.GetUnderlyingType(type);
            if (underlying is not null)
            {
                return Core(underlying, null, reachedFrom) + " | null";
            }
            string core = Core(type, nullability, reachedFrom);
            return !type.IsValueType && nullability?.ReadState == NullabilityState.Nullable ? core + " | null" : core;
        }

        private string Core(Type type, NullabilityInfo? nullability, string reachedFrom)
        {
            if (StringTypes.Contains(type))
            {
                return "string";
            }
            if (NumberTypes.Contains(type))
            {
                return "number";
            }
            if (type == typeof(bool))
            {
                return "boolean";
            }
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                {
                    throw new TypeScriptGenerationException($"{reachedFrom} is a multi-dimensional array; the TypeScript target maps only one-dimensional arrays.");
                }
                return Sequence(type.GetElementType()!, nullability?.ElementType, reachedFrom);
            }
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                Type[] arguments = type.GetGenericArguments();
                if (SequenceDefinitions.Contains(definition))
                {
                    return Sequence(arguments[0], Argument(nullability, 0), reachedFrom);
                }
                if (DictionaryDefinitions.Contains(definition))
                {
                    if (arguments[0] != typeof(string))
                    {
                        throw new TypeScriptGenerationException($"{reachedFrom} is a dictionary keyed by {arguments[0].Name}; the TypeScript target maps only string-keyed dictionaries.");
                    }
                    return $"Readonly<Record<string, {Map(arguments[1], Argument(nullability, 1), reachedFrom)}>>";
                }
            }
            return Declare(type, reachedFrom);
        }

        private string Sequence(Type element, NullabilityInfo? nullability, string reachedFrom)
        {
            string mapped = Map(element, nullability, reachedFrom);
            return mapped.Contains(' ', StringComparison.Ordinal) ? $"readonly ({mapped})[]" : $"readonly {mapped}[]";
        }

        private static NullabilityInfo? Argument(NullabilityInfo? nullability, int index) =>
            nullability is not null && nullability.GenericTypeArguments.Length > index ? nullability.GenericTypeArguments[index] : null;
    }
}
