using System.Reflection;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Names decorators the way the generator does, and finds the ones generated ahead of time among the
    /// loaded assemblies. Shared by <see cref="DecoratorFactory"/> here and by the generator and the
    /// <c>ServiceRegistry</c> resolver in <c>bam.generators.decorators</c>, so the naming rule exists once.
    /// </summary>
    public static class GeneratedDecorators
    {
        /// <summary>The suffix a generated decorator's type name carries: <c>EchoServiceDecorator</c>.</summary>
        public const string TypeNameSuffix = "Decorator";

        /// <summary>The suffix appended to the implementation's namespace: <c>My.Services.Decorators</c>.</summary>
        public const string NamespaceSuffix = ".Decorators";

        /// <summary>The namespace used when the implementation type has none.</summary>
        public const string DefaultNamespace = "Bam.Generated";

        /// <summary>Gets the namespace a decorator for <paramref name="implementationType"/> is generated into.</summary>
        public static string NamespaceOf(Type implementationType)
        {
            Guard.NotNull(implementationType, nameof(implementationType));

            return (implementationType.Namespace ?? DefaultNamespace) + NamespaceSuffix;
        }

        /// <summary>Gets the simple type name of the decorator generated for <paramref name="implementationType"/>.</summary>
        public static string TypeNameOf(Type implementationType)
        {
            Guard.NotNull(implementationType, nameof(implementationType));

            return SimpleName(implementationType) + TypeNameSuffix;
        }

        /// <summary>Gets the full name of the decorator generated for <paramref name="implementationType"/>.</summary>
        public static string FullNameOf(Type implementationType)
        {
            return NamespaceOf(implementationType) + "." + TypeNameOf(implementationType);
        }

        /// <summary>
        /// Names a type the way generated identifiers do: the name without its arity tick, with generic
        /// arguments spelled out (<c>RepositoryOfPerson</c> for <c>Repository&lt;Person&gt;</c>).
        /// </summary>
        public static string SimpleName(Type type)
        {
            Guard.NotNull(type, nameof(type));

            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick < 0)
            {
                return name;
            }

            return name.Substring(0, tick) + "Of" + string.Concat(type.GetGenericArguments().Select(SimpleName));
        }

        /// <summary>
        /// Finds every loaded type that can stand in for <paramref name="interfaceType"/> as a decorator of
        /// <paramref name="implementationType"/>: a concrete class extending
        /// <c>Decorator&lt;I, T&gt;</c>, implementing <c>I</c>, with a public
        /// <c>(T, IDecoratorLogger)</c> constructor. Only assemblies that reference the runtime are searched.
        /// </summary>
        /// <exception cref="ArgumentException">The pair cannot close <c>Decorator&lt;I, T&gt;</c>.</exception>
        public static IReadOnlyList<Type> FindAll(Type interfaceType, Type implementationType)
        {
            Guard.NotNull(interfaceType, nameof(interfaceType));
            Guard.NotNull(implementationType, nameof(implementationType));

            Type baseType = typeof(Decorator<,>).MakeGenericType(interfaceType, implementationType);
            string? runtimeName = typeof(Decorator<,>).Assembly.GetName().Name;
            List<Type> candidates = new List<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || !assembly.GetReferencedAssemblies().Any(reference => reference.Name == runtimeName))
                {
                    continue;
                }

                candidates.AddRange(TypesOf(assembly).Where(type =>
                    type.IsClass
                    && !type.IsAbstract
                    && baseType.IsAssignableFrom(type)
                    && interfaceType.IsAssignableFrom(type)
                    && type.GetConstructor(new Type[] { implementationType, typeof(IDecoratorLogger) }) != null));
            }

            return candidates;
        }

        /// <summary>
        /// Finds the generated decorator for the pair, or null when none is loaded. When several qualify,
        /// <see cref="Prefer"/> picks one.
        /// </summary>
        /// <exception cref="ArgumentException">The pair cannot close <c>Decorator&lt;I, T&gt;</c>.</exception>
        public static Type? Find(Type interfaceType, Type implementationType)
        {
            return Prefer(FindAll(interfaceType, implementationType), implementationType);
        }

        /// <summary>
        /// Picks the decorator to use for <paramref name="implementationType"/> among
        /// <paramref name="candidates"/>, or null when there are none. In order: a candidate at the
        /// conventional name (<see cref="FullNameOf"/>) over one at another name; one declared in the
        /// implementation's own assembly over one from another assembly; one from an assembly loaded from
        /// disk (a non-empty <see cref="Assembly.Location"/>) over one loaded from bytes; then the first in
        /// <paramref name="candidates"/>. The pick does not depend on load order except in the last step,
        /// which also decides under a single-file publish, where every bundled assembly reports an empty
        /// <see cref="Assembly.Location"/>.
        /// </summary>
        /// <param name="candidates">The qualifying decorator types, as <see cref="FindAll"/> returns them.</param>
        /// <param name="implementationType">The implementation type the decorator wraps.</param>
        /// <returns>The preferred candidate, or null when <paramref name="candidates"/> is empty.</returns>
        public static Type? Prefer(IReadOnlyList<Type> candidates, Type implementationType)
        {
            Guard.NotNull(candidates, nameof(candidates));
            Guard.NotNull(implementationType, nameof(implementationType));

            if (candidates.Count < 2)
            {
                return candidates.Count == 0 ? null : candidates[0];
            }

            string conventionalName = FullNameOf(implementationType);
            return candidates
                .OrderBy(type => type.FullName == conventionalName ? 0 : 1)
                .ThenBy(type => type.Assembly == implementationType.Assembly ? 0 : 1)
                .ThenBy(type => string.IsNullOrEmpty(type.Assembly.Location) ? 1 : 0)
                .First();
        }

        private static IEnumerable<Type> TypesOf(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null).Select(type => type!);
            }
        }
    }
}
