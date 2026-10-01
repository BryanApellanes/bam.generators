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
/// Nullable annotations are read from what the compiler wrote: the <c>NullableAttribute</c> on the member,
/// or the <c>NullableContextAttribute</c> that applies to it, laid out one flag per type in declaration
/// order. Reflection's own nullability API is not used, because it cannot tell <c>T</c> from <c>T?</c> when
/// nothing constrains <c>T</c>.
/// </para>
/// <para>
/// Annotations belong to the member's declaration, so for a member of a closed generic type they are read
/// from the open type: <c>IBox&lt;string&gt;.Unbox()</c> is annotated the way <c>IBox&lt;T&gt;.Unbox()</c>
/// was written, and a <c>T?</c> there renders as <c>string?</c>.
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
        ArgumentNullException.ThrowIfNull(type);

        return Render(type, type, NullableFlags.None);
    }

    /// <summary>
    /// Gets the nullable-annotated C# name of <paramref name="parameter"/>'s type. Modifiers
    /// (<c>ref</c>/<c>out</c>/<c>in</c>) are not included.
    /// </summary>
    public static string Of(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        ParameterInfo declared = DeclarationOf(parameter);
        return Render(parameter.ParameterType, declared.ParameterType, NullableFlags.For(declared.GetCustomAttributesData(), declared.Member));
    }

    /// <summary>Gets the nullable-annotated C# name of <paramref name="property"/>'s type.</summary>
    public static string Of(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        PropertyInfo declared = DeclarationOf(property);
        MemberInfo scope = declared.GetMethod ?? declared.SetMethod ?? (MemberInfo)declared;
        return Render(property.PropertyType, declared.PropertyType, NullableFlags.For(declared.GetCustomAttributesData(), scope));
    }

    /// <summary>Gets the nullable-annotated C# name of <paramref name="eventInfo"/>'s handler type.</summary>
    public static string Of(EventInfo eventInfo)
    {
        ArgumentNullException.ThrowIfNull(eventInfo);

        EventInfo declared = DeclarationOf(eventInfo);
        Type handlerType = eventInfo.EventHandlerType ?? typeof(EventHandler);
        return Render(handlerType, declared.EventHandlerType ?? handlerType, NullableFlags.For(declared.GetCustomAttributesData(), declared));
    }

    /// <summary>Gets the nullable-annotated C# name of <paramref name="method"/>'s return type.</summary>
    public static string OfReturn(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (method.ReturnType == typeof(void))
        {
            return "void";
        }

        MethodInfo declared = DeclarationOf(method);
        return Render(method.ReturnType, declared.ReturnType, NullableFlags.For(declared.ReturnParameter.GetCustomAttributesData(), declared));
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
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(resultType);

        if (resultType == method.ReturnType)
        {
            return OfReturn(method);
        }

        MethodInfo declared = DeclarationOf(method);
        Type[] declaredArguments = declared.ReturnType.IsGenericType ? declared.ReturnType.GetGenericArguments() : Type.EmptyTypes;
        NullableFlags flags = NullableFlags.For(declared.ReturnParameter.GetCustomAttributesData(), declared);

        // The task's own flag comes first; the argument's follow it.
        flags.Next();
        return Render(resultType, declaredArguments.Length == 1 ? declaredArguments[0] : resultType, flags);
    }

    /// <summary>
    /// Gets the constraint clauses an explicit interface implementation or an override of
    /// <paramref name="method"/> has to restate, with a leading space; empty for a method that isn't generic.
    /// Such a member inherits its constraints, but <c>T?</c> reads differently for a reference type than for
    /// a value type, so each generic parameter needs <c>class</c>, <c>struct</c> or <c>default</c> said again.
    /// </summary>
    public static string GenericConstraintsOf(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

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

    // Names `actual` while walking `declared` — the same type as the member declared it, which differs from
    // `actual` only where the declaring type's generic parameters were substituted — consuming the compiler's
    // nullable flags in the order it wrote them: one per reference type, array, pointer and generic
    // parameter, none for a plain value type, in declaration order with generic arguments after their type.
    // A pointer's flag is never consumed here, since nothing nullable can follow a pointer in a type.
    private static string Render(Type actual, Type declared, NullableFlags flags)
    {
        if (actual.IsByRef)
        {
            return Render(actual.GetElementType()!, declared.IsByRef ? declared.GetElementType()! : declared, flags);
        }

        if (actual == typeof(void))
        {
            return "void";
        }

        if (declared.IsGenericParameter)
        {
            // A parameter of the declaring type stands for whatever was substituted; what was substituted
            // carries no flags of its own. A parameter of the method is named as itself.
            byte flag = flags.Next();
            string name = actual.IsGenericParameter ? actual.Name : Render(actual, actual, NullableFlags.None);
            return flag == Annotated && !actual.IsValueType ? name + "?" : name;
        }

        if (actual.IsArray)
        {
            byte flag = flags.Next();
            string rank = new string(',', actual.GetArrayRank() - 1);
            string element = Render(actual.GetElementType()!, declared.IsArray ? declared.GetElementType()! : actual.GetElementType()!, flags);
            return flag == Annotated ? $"{element}[{rank}]?" : $"{element}[{rank}]";
        }

        if (actual.IsPointer)
        {
            return Render(actual.GetElementType()!, declared.IsPointer ? declared.GetElementType()! : actual.GetElementType()!, flags) + "*";
        }

        Type[] arguments = actual.IsGenericType ? actual.GetGenericArguments() : Type.EmptyTypes;
        Type[] declaredArguments = declared.IsGenericType && declared.GetGenericArguments().Length == arguments.Length ? declared.GetGenericArguments() : arguments;

        bool annotated = false;
        if (!actual.IsValueType)
        {
            annotated = flags.Next() == Annotated;
        }
        else if (actual.IsGenericType && actual.GetGenericTypeDefinition() != typeof(Nullable<>))
        {
            flags.Next(); // a generic value type takes a flag of its own, always 0
        }

        string[] renderedArguments = new string[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            renderedArguments[i] = Render(arguments[i], declaredArguments[i], flags);
        }

        string qualified = QualifiedNameOf(actual, renderedArguments);
        return annotated ? qualified + "?" : qualified;
    }

    // Names a type level by level, outermost first, handing each level the generic arguments it declares.
    // A nested type carries its outer types' arguments as well as its own, so Outer<int>.Inner<string> has
    // two arguments: the first belongs to Outer, the second to Inner.
    private static string QualifiedNameOf(Type type, string[] renderedArguments)
    {
        List<Type> levels = new List<Type>();
        for (Type? level = type; level != null; level = level.DeclaringType)
        {
            levels.Insert(0, level);
        }

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

            int declaredSoFar = Math.Min(levels[i].IsGenericType ? levels[i].GetGenericArguments().Length : 0, renderedArguments.Length);
            if (declaredSoFar > named)
            {
                name.Append('<').Append(string.Join(", ", renderedArguments, named, declaredSoFar - named)).Append('>');
                named = declaredSoFar;
            }
        }

        return name.ToString();
    }

    // A base-class constraint makes T a reference type. Enum and ValueType are classes themselves but
    // constrain T to types that are not.
    private static bool IsReferenceTypeConstraint(Type constraint)
    {
        return constraint.IsClass && constraint != typeof(Enum) && constraint != typeof(ValueType);
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

    /// <summary>
    /// The compiler's nullable flags for one type as written on a member: either one flag per type in the
    /// type's structure, read in order, or a single flag that applies to all of them.
    /// </summary>
    private sealed class NullableFlags
    {
        private readonly byte[]? _flags;
        private readonly byte _everywhere;
        private int _index;

        private NullableFlags(byte[]? flags, byte everywhere)
        {
            _flags = flags;
            _everywhere = everywhere;
        }

        /// <summary>Flags for a type with no member: nothing is annotated.</summary>
        public static NullableFlags None => new NullableFlags(null, 0);

        /// <summary>
        /// Reads the flags from a member's <c>NullableAttribute</c>, falling back to the
        /// <c>NullableContextAttribute</c> of the member or the nearest declaring type that has one.
        /// </summary>
        public static NullableFlags For(IList<CustomAttributeData> attributes, MemberInfo? scope)
        {
            CustomAttributeData? attribute = attributes.FirstOrDefault(candidate => candidate.AttributeType.FullName == NullableAttributeName);
            if (attribute != null && attribute.ConstructorArguments.Count == 1)
            {
                object? value = attribute.ConstructorArguments[0].Value;
                if (value is byte everywhere)
                {
                    return new NullableFlags(null, everywhere);
                }

                if (value is IReadOnlyList<CustomAttributeTypedArgument> list)
                {
                    return new NullableFlags(list.Select(item => item.Value as byte? ?? 0).ToArray(), 0);
                }
            }

            return new NullableFlags(null, ContextOf(scope));
        }

        /// <summary>Gets the next flag in declaration order.</summary>
        public byte Next()
        {
            if (_flags == null)
            {
                return _everywhere;
            }

            return _index < _flags.Length ? _flags[_index++] : _everywhere;
        }

        private static byte ContextOf(MemberInfo? member)
        {
            for (MemberInfo? scope = member; scope != null; scope = scope.DeclaringType)
            {
                CustomAttributeData? attribute = scope.GetCustomAttributesData().FirstOrDefault(candidate => candidate.AttributeType.FullName == NullableContextAttributeName);
                if (attribute != null && attribute.ConstructorArguments.Count == 1 && attribute.ConstructorArguments[0].Value is byte context)
                {
                    return context;
                }
            }

            return 0;
        }
    }
}
