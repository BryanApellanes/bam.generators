using System.Reflection;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Finds the exact method a generated decorator member implements. Generated code calls
    /// <see cref="Find"/> once per method, from a static field initializer, and hands the result to the
    /// decorator base so handlers see the overload that actually ran.
    /// </summary>
    public static class DecoratedMethod
    {
        /// <summary>
        /// Finds the method declared by <paramref name="declaringType"/> with the given name, generic arity and
        /// parameter types.
        /// </summary>
        /// <param name="declaringType">The interface declaring the method.</param>
        /// <param name="name">The method's name.</param>
        /// <param name="genericArity">The number of generic parameters the method declares.</param>
        /// <param name="parameterTypes">The signature of each parameter type, as <see cref="SignatureOf"/> renders it.</param>
        /// <returns>
        /// The method, or null when the type declares none that matches — which means the generated decorator
        /// is older than the interface it was generated from. The decorator still works; handlers are then
        /// given the method found by name.
        /// </returns>
        public static MethodInfo? Find(Type declaringType, string name, int genericArity, params string[] parameterTypes)
        {
            ArgumentNullException.ThrowIfNull(declaringType);
            ArgumentNullException.ThrowIfNull(name);

            string[] expected = parameterTypes ?? Array.Empty<string>();
            foreach (MethodInfo method in declaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.Name != name || method.GetGenericArguments().Length != genericArity)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == expected.Length && parameters.Select(parameter => SignatureOf(parameter.ParameterType)).SequenceEqual(expected))
                {
                    return method;
                }
            }

            return null;
        }

        /// <summary>
        /// Renders a parameter type as the text <see cref="Find"/> compares. The generator and the runtime
        /// both call this, so the two always agree.
        /// </summary>
        /// <param name="parameterType">The parameter's type.</param>
        public static string SignatureOf(Type parameterType)
        {
            ArgumentNullException.ThrowIfNull(parameterType);

            return parameterType.ToString();
        }
    }
}
