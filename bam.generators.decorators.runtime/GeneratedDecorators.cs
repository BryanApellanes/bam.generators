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
        /// Finds the generated decorator for the pair, or null when none is loaded. When several qualify, the
        /// one at the conventional name (<see cref="FullNameOf"/>) wins, then the first found.
        /// </summary>
        /// <exception cref="ArgumentException">The pair cannot close <c>Decorator&lt;I, T&gt;</c>.</exception>
        public static Type? Find(Type interfaceType, Type implementationType)
        {
            IReadOnlyList<Type> candidates = FindAll(interfaceType, implementationType);
            if (candidates.Count < 2)
            {
                return candidates.FirstOrDefault();
            }

            string conventionalName = FullNameOf(implementationType);
            return candidates.FirstOrDefault(type => type.FullName == conventionalName) ?? candidates[0];
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
