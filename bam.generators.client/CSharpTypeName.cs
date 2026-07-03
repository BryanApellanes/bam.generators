namespace Bam.Generators;

/// <summary>
/// Renders fully-qualified, compilable C# type names (with a <c>global::</c> prefix) for use in generated source.
/// Fully-qualifying avoids depending on <c>using</c> directives in the generated file.
/// </summary>
internal static class CSharpTypeName
{
    /// <summary>Gets the fully-qualified C# name for <paramref name="type"/>.</summary>
    public static string Of(Type type)
    {
        if (type == typeof(void))
        {
            return "void";
        }

        if (type.IsArray)
        {
            return Of(type.GetElementType()!) + "[]";
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
            string args = string.Join(", ", type.GetGenericArguments().Select(Of));
            return $"global::{name}<{args}>";
        }

        string full = (type.FullName ?? type.Name).Replace('+', '.');
        return "global::" + full;
    }
}
