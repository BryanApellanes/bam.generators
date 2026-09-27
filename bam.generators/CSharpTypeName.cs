using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;

namespace Bam.Generators;

/// <summary>
/// Renders fully-qualified, compilable C# type names (with a <c>global::</c> prefix) for use in generated source.
/// Fully-qualifying avoids depending on <c>using</c> directives in the generated file. Shared by every
/// reflection-driven generator in <see cref="Bam.Generators"/> (service clients, decorators).
/// </summary>
public static class CSharpTypeName
{
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
    /// <param name="nullability">Nullable-annotation information for the type; null renders no annotations.</param>
    public static string Of(Type type, NullabilityInfo? nullability)
    {
        if (type.IsByRef)
        {
            return Of(type.GetElementType()!, nullability?.ElementType ?? nullability);
        }

        string name = NameOf(type, nullability);
        if (nullability != null && !type.IsValueType && !type.IsGenericParameter && IsNullable(nullability))
        {
            name += "?";
        }

        return name;
    }

    /// <summary>
    /// Gets the nullable-annotated C# name of <paramref name="parameter"/>'s type. Modifiers
    /// (<c>ref</c>/<c>out</c>/<c>in</c>) are not included.
    /// </summary>
    public static string Of(ParameterInfo parameter)
    {
        return Of(parameter.ParameterType, CreateNullability(context => context.Create(parameter)));
    }

    /// <summary>Gets the nullable-annotated C# name of <paramref name="property"/>'s type.</summary>
    public static string Of(PropertyInfo property)
    {
        return Of(property.PropertyType, CreateNullability(context => context.Create(property)));
    }

    /// <summary>Gets the nullable-annotated C# name of <paramref name="eventInfo"/>'s handler type.</summary>
    public static string Of(EventInfo eventInfo)
    {
        return Of(eventInfo.EventHandlerType ?? typeof(EventHandler), CreateNullability(context => context.Create(eventInfo)));
    }

    /// <summary>Gets the nullable-annotated C# name of <paramref name="method"/>'s return type.</summary>
    public static string OfReturn(MethodInfo method)
    {
        return Of(method.ReturnType, ReturnNullability(method));
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
        NullabilityInfo? nullability = ReturnNullability(method);
        if (nullability != null && resultType != method.ReturnType)
        {
            nullability = nullability.GenericTypeArguments.Length == 1 ? nullability.GenericTypeArguments[0] : null;
        }

        return Of(resultType, nullability);
    }

    /// <summary>
    /// Makes <paramref name="name"/> safe to use as a C# identifier by prefixing reserved keywords with <c>@</c>
    /// (a parameter named <c>event</c> renders as <c>@event</c>).
    /// </summary>
    public static string Identifier(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
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

        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            string name = definition.FullName ?? definition.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0)
            {
                name = name.Substring(0, tick);
            }
            name = name.Replace('+', '.');
            Type[] arguments = type.GetGenericArguments();
            NullabilityInfo[]? argumentNullability = nullability?.GenericTypeArguments;
            bool annotated = argumentNullability != null && argumentNullability.Length == arguments.Length;
            string args = string.Join(", ", arguments.Select((argument, index) => Of(argument, annotated ? argumentNullability![index] : null)));
            return $"global::{name}<{args}>";
        }

        string full = (type.FullName ?? type.Name).Replace('+', '.');
        return "global::" + full;
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
