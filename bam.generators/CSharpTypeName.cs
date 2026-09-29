using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Bam.Generators;

/// <summary>
/// Renders fully-qualified, compilable C# type names (with a <c>global::</c> prefix) for use in generated source.
/// Fully-qualifying avoids depending on <c>using</c> directives in the generated file. Shared by every
/// reflection-driven generator in <see cref="Bam.Generators"/> (service clients, decorators).
/// </summary>
/// <remarks>
/// <para>
/// Nullable annotations are read from the member's declaration. For a member of a closed generic type that
/// means the member of the open type: <c>IBox&lt;string&gt;.Unbox()</c> is annotated the way
/// <c>IBox&lt;T&gt;.Unbox()</c> was written, because the closed type carries no annotations of its own.
/// </para>
/// <para>
/// A generic parameter with nothing constraining it is the one case reflection's nullability API cannot
/// settle: it reports <c>T</c> and <c>T?</c> alike as nullable. Where such a parameter is the whole type of
/// a parameter, a return or a task's result, the compiler's own annotation is read instead. Where it sits
/// deeper (<c>List&lt;T?&gt;</c>) it is rendered unannotated.
/// </para>
/// </remarks>
public static class CSharpTypeName
{
    private const string NullableAttributeName = "System.Runtime.CompilerServices.NullableAttribute";
    private const string NullableContextAttributeName = "System.Runtime.CompilerServices.NullableContextAttribute";
    private const byte Annotated = 2;
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>Gets the fully-qualified C# name for <paramref name="type"/>, without nullable annotations.</summary>
    /// <param name="type">The type to name. By-ref types (<c>ref</c>/<c>out</c>/<c>in</c>) name their element type.</param>
    public static string Of(Type type)
    {
        return Of(type, null);
    }

    /// <summary>
    /// Gets the fully-qualified C# name for <paramref name="type"/>, appending <c>?</c> to reference types that
    /// <paramref name="nullability"/> reports as nullable (recursing into generic arguments and array elements).
    /// </summary>
    /// <param name="type">The type to name. By-ref types name their element type.</param>
    /// <param name="nullability">
    /// Nullable-annotation information for the type; null renders no annotations. It may describe the open
    /// form of <paramref name="type"/>, with a generic parameter where <paramref name="type"/> has an argument.
    /// </param>
    public static string Of(Type type, NullabilityInfo? nullability)
    {
        if (type.IsByRef)
        {
            return Of(type.GetElementType()!, nullability?.ElementType ?? nullability);
        }

        Type? described = nullability == null ? null : ElementOf(nullability.Type);
        bool substituted = described != null && described.IsGenericParameter && !type.IsGenericParameter;

        // What was substituted for a generic parameter has no annotations of its own to descend into.
        string name = NameOf(type, substituted ? null : nullability);
        if (nullability != null && !type.IsValueType && IsNullable(nullability) && CanBeTrusted(described))
        {
            name += "?";
        }

        return name;
    }

    /// <summary>
    /// Gets the constraint clauses an explicit interface implementation or an override of
    /// <paramref name="method"/> has to restate, with a leading space; empty for a method that isn't generic.
    /// Such a member inherits its constraints, but <c>T?</c> reads differently for a reference type than for
    /// a value type, so each generic parameter needs <c>class</c>, <c>struct</c> or <c>default</c> said again.
    /// </summary>
    public static string GenericConstraintsOf(MethodInfo method)
    {
        if (!method.IsGenericMethodDefinition)
        {
            return string.Empty;
        }

        StringBuilder clauses = new StringBuilder();
        foreach (Type parameter in method.GetGenericArguments())
        {
            GenericParameterAttributes attributes = parameter.GenericParameterAttributes;
            string constraint = "default";
            if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                constraint = "struct";
            }
            else if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0 || parameter.GetGenericParameterConstraints().Any(IsReferenceTypeConstraint))
            {
                constraint = "class";
            }

            clauses.Append(" where ").Append(parameter.Name).Append(" : ").Append(constraint);
        }

        return clauses.ToString();
    }

    /// <summary>
    /// Gets the nullable-annotated C# name of <paramref name="parameter"/>'s type. Modifiers
    /// (<c>ref</c>/<c>out</c>/<c>in</c>) are not included.
    /// </summary>
    public static string Of(ParameterInfo parameter)
    {
        ParameterInfo declared = DeclarationOf(parameter);
        return Annotate(
            parameter.ParameterType,
            CreateNullability(context => context.Create(declared)),
            declared.ParameterType,
            declared.GetCustomAttributesData(),
            declared.Member,
            0);
    }

    /// <summary>Gets the nullable-annotated C# name of <paramref name="property"/>'s type.</summary>
    public static string Of(PropertyInfo property)
    {
        PropertyInfo declared = DeclarationOf(property);
        return Annotate(
            property.PropertyType,
            CreateNullability(context => context.Create(declared)),
            declared.PropertyType,
            declared.GetCustomAttributesData(),
            declared.GetMethod ?? declared.SetMethod ?? (MemberInfo)declared,
            0);
    }

    /// <summary>Gets the nullable-annotated C# name of <paramref name="eventInfo"/>'s handler type.</summary>
    public static string Of(EventInfo eventInfo)
    {
        EventInfo declared = DeclarationOf(eventInfo);
        Type handlerType = eventInfo.EventHandlerType ?? typeof(EventHandler);
        return Annotate(
            handlerType,
            CreateNullability(context => context.Create(declared)),
            declared.EventHandlerType ?? handlerType,
            declared.GetCustomAttributesData(),
            declared,
            0);
    }

    /// <summary>Gets the nullable-annotated C# name of <paramref name="method"/>'s return type.</summary>
    public static string OfReturn(MethodInfo method)
    {
        MethodInfo declared = DeclarationOf(method);
        return Annotate(
            method.ReturnType,
            ReturnNullability(declared),
            declared.ReturnType,
            declared.ReturnParameter.GetCustomAttributesData(),
            declared,
            0);
    }

    /// <summary>
    /// Gets the nullable-annotated C# name of the value <paramref name="method"/> produces: the return type
    /// itself, or the <c>T</c> of a <c>Task&lt;T&gt;</c> / <c>ValueTask&lt;T&gt;</c> when
    /// <paramref name="resultType"/> is that generic argument.
    /// </summary>
    /// <param name="method">The method whose result is being named.</param>
    /// <param name="resultType">The result type — either the return type or its single generic argument.</param>
    public static string OfResult(MethodInfo method, Type resultType)
    {
        if (resultType == method.ReturnType)
        {
            return OfReturn(method);
        }

        MethodInfo declared = DeclarationOf(method);
        NullabilityInfo? nullability = ReturnNullability(declared);
        nullability = nullability != null && nullability.GenericTypeArguments.Length == 1 ? nullability.GenericTypeArguments[0] : null;
        Type[] declaredArguments = declared.ReturnType.IsGenericType ? declared.ReturnType.GetGenericArguments() : Type.EmptyTypes;

        // The compiler lists annotations outermost first: the task's own, then its argument's.
        return Annotate(
            resultType,
            nullability,
            declaredArguments.Length == 1 ? declaredArguments[0] : resultType,
            declared.ReturnParameter.GetCustomAttributesData(),
            declared,
            1);
    }

    /// <summary>
    /// Renders <paramref name="value"/> as a C# string literal, quoted and escaped, for writing text taken
    /// from metadata into generated source.
    /// </summary>
    public static string Literal(string value)
    {
        return SymbolDisplay.FormatLiteral(value, true);
    }

    /// <summary>
    /// Makes <paramref name="name"/> safe to use as a C# identifier by prefixing reserved keywords with <c>@</c>
    /// (a parameter named <c>event</c> renders as <c>@event</c>).
    /// </summary>
    public static string Identifier(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
    }

    // Names the type, then settles the one case Of(Type, NullabilityInfo) leaves open: a generic parameter
    // with nothing constraining it, standing as the whole type.
    private static string Annotate(Type type, NullabilityInfo? nullability, Type declaredType, IList<CustomAttributeData> attributes, MemberInfo member, int position)
    {
        string name = Of(type, nullability);
        Type declared = ElementOf(declaredType);
        Type actual = ElementOf(type);
        if (!declared.IsGenericParameter || CanBeTrusted(declared) || actual.IsValueType || name.EndsWith('?'))
        {
            return name;
        }

        byte? flag = NullableFlag(attributes, position) ?? NullableContext(member);
        return flag == Annotated ? name + "?" : name;
    }

    // Reflection reports whether null is allowed for a generic parameter from its constraints as well as
    // its annotation. With nothing constraining it the answer is always "allowed", annotated or not.
    private static bool CanBeTrusted(Type? described)
    {
        if (described == null || !described.IsGenericParameter)
        {
            return true;
        }

        return (described.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0
            || described.GetGenericParameterConstraints().Length > 0;
    }

    private static Type ElementOf(Type type)
    {
        return type.IsByRef ? type.GetElementType()! : type;
    }

    // A base-class constraint makes T a reference type. Enum and ValueType are classes themselves but
    // constrain T to types that are not.
    private static bool IsReferenceTypeConstraint(Type constraint)
    {
        return constraint.IsClass && constraint != typeof(Enum) && constraint != typeof(ValueType);
    }

    private static byte? NullableFlag(IList<CustomAttributeData> attributes, int position)
    {
        CustomAttributeData? attribute = attributes.FirstOrDefault(candidate => candidate.AttributeType.FullName == NullableAttributeName);
        if (attribute == null || attribute.ConstructorArguments.Count != 1)
        {
            return null;
        }

        object? value = attribute.ConstructorArguments[0].Value;
        if (value is byte everyPosition)
        {
            return everyPosition;
        }

        if (value is IReadOnlyList<CustomAttributeTypedArgument> byPosition && position < byPosition.Count)
        {
            return byPosition[position].Value as byte?;
        }

        return null;
    }

    // The annotation that applies where a member states none: its own context, else its type's, outward.
    private static byte? NullableContext(MemberInfo? member)
    {
        for (MemberInfo? scope = member; scope != null; scope = scope.DeclaringType)
        {
            CustomAttributeData? attribute = scope.GetCustomAttributesData().FirstOrDefault(candidate => candidate.AttributeType.FullName == NullableContextAttributeName);
            if (attribute != null && attribute.ConstructorArguments.Count == 1)
            {
                return attribute.ConstructorArguments[0].Value as byte?;
            }
        }

        return null;
    }

    private static MethodInfo DeclarationOf(MethodInfo method)
    {
        Type? openType = OpenTypeOf(method);
        return openType?.GetMethods(Declared).FirstOrDefault(candidate => candidate.HasSameMetadataDefinitionAs(method)) ?? method;
    }

    private static PropertyInfo DeclarationOf(PropertyInfo property)
    {
        Type? openType = OpenTypeOf(property);
        return openType?.GetProperties(Declared).FirstOrDefault(candidate => candidate.HasSameMetadataDefinitionAs(property)) ?? property;
    }

    private static EventInfo DeclarationOf(EventInfo eventInfo)
    {
        Type? openType = OpenTypeOf(eventInfo);
        return openType?.GetEvents(Declared).FirstOrDefault(candidate => candidate.HasSameMetadataDefinitionAs(eventInfo)) ?? eventInfo;
    }

    private static ParameterInfo DeclarationOf(ParameterInfo parameter)
    {
        ParameterInfo[] declared = parameter.Member switch
        {
            MethodInfo method => DeclarationOf(method).GetParameters(),
            PropertyInfo property => DeclarationOf(property).GetIndexParameters(),
            _ => Array.Empty<ParameterInfo>()
        };

        return parameter.Position >= 0 && parameter.Position < declared.Length ? declared[parameter.Position] : parameter;
    }

    // The open form of the member's declaring type, when the member belongs to a closed generic type.
    private static Type? OpenTypeOf(MemberInfo member)
    {
        Type? declaringType = member.DeclaringType;
        return declaringType != null && declaringType.IsConstructedGenericType ? declaringType.GetGenericTypeDefinition() : null;
    }

    private static string NameOf(Type type, NullabilityInfo? nullability)
    {
        if (type == typeof(void))
        {
            return "void";
        }

        if (type.IsGenericParameter)
        {
            return type.Name; // a method/type parameter (T) is referenced by its bare name
        }

        if (type.IsArray)
        {
            string rank = new string(',', type.GetArrayRank() - 1);
            return Of(type.GetElementType()!, nullability?.ElementType) + "[" + rank + "]";
        }

        return QualifiedNameOf(type, nullability);
    }

    // Names a type level by level, outermost first, handing each level the generic arguments it declares.
    // A nested type carries its outer types' arguments as well as its own, so Outer<int>.Inner<string> has
    // two arguments: the first belongs to Outer, the second to Inner.
    private static string QualifiedNameOf(Type type, NullabilityInfo? nullability)
    {
        List<Type> levels = new List<Type>();
        for (Type? level = type; level != null; level = level.DeclaringType)
        {
            levels.Insert(0, level);
        }

        Type[] arguments = type.GetGenericArguments();
        NullabilityInfo[]? argumentNullability = nullability?.GenericTypeArguments;
        bool annotated = argumentNullability != null && argumentNullability.Length == arguments.Length;

        StringBuilder name = new StringBuilder("global::");
        if (!string.IsNullOrEmpty(levels[0].Namespace))
        {
            name.Append(levels[0].Namespace).Append('.');
        }

        int named = 0;
        for (int i = 0; i < levels.Count; i++)
        {
            if (i > 0)
            {
                name.Append('.');
            }

            string simpleName = levels[i].Name;
            int tick = simpleName.IndexOf('`');
            name.Append(tick >= 0 ? simpleName.Substring(0, tick) : simpleName);

            int declaredSoFar = Math.Min(levels[i].IsGenericType ? levels[i].GetGenericArguments().Length : 0, arguments.Length);
            if (declaredSoFar > named)
            {
                IEnumerable<string> own = Enumerable
                    .Range(named, declaredSoFar - named)
                    .Select(index => Of(arguments[index], annotated ? argumentNullability![index] : null));
                name.Append('<').Append(string.Join(", ", own)).Append('>');
                named = declaredSoFar;
            }
        }

        return name.ToString();
    }

    private static bool IsNullable(NullabilityInfo nullability)
    {
        return nullability.ReadState == NullabilityState.Nullable || nullability.WriteState == NullabilityState.Nullable;
    }

    private static NullabilityInfo? ReturnNullability(MethodInfo method)
    {
        if (method.ReturnType == typeof(void))
        {
            return null;
        }

        return CreateNullability(context => context.Create(method.ReturnParameter));
    }

    // NullabilityInfoContext is not thread-safe and caches per instance, so each lookup gets its own.
    // Metadata it cannot interpret renders without annotations rather than failing generation.
    private static NullabilityInfo? CreateNullability(Func<NullabilityInfoContext, NullabilityInfo> create)
    {
        try
        {
            return create(new NullabilityInfoContext());
        }
        catch (Exception)
        {
            return null;
        }
    }
}
