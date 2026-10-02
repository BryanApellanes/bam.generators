using Bam.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// The default <see cref="IDecoratorTypeResolver"/>. Uses a decorator that was generated ahead of time when
    /// one is loaded; otherwise generates the source with <see cref="DecoratorGenerator"/>, compiles it with
    /// <see cref="RoslynCompiler"/> and loads the result. Each pair is resolved once and cached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runtime compilation needs the assemblies of the service types on disk, and costs a compile on first
    /// use. Generate decorators ahead of time (and reference them) to avoid both.
    /// </para>
    /// <para>
    /// What gets compiled is loaded into the process, so the default resolver renders from the templates
    /// embedded in this assembly and nothing else: no template directory is read or created. A resolver given
    /// its own <see cref="DecoratorGenerator"/> compiles whatever that generator renders; pass one that reads
    /// templates from disk only when that directory is as trusted as the application's own binaries.
    /// </para>
    /// </remarks>
    public class DecoratorTypeResolver : IDecoratorTypeResolver
    {
        private static readonly Lazy<DecoratorTypeResolver> _default = new Lazy<DecoratorTypeResolver>(() => new DecoratorTypeResolver());

        private readonly ConcurrentDictionary<DecoratorKey, Lazy<Type>> _types;
        private readonly Lazy<DecoratorGenerator> _generator;

        /// <summary>
        /// Initializes a new instance that renders from the embedded templates only. The generator is not
        /// created until a decorator actually has to be compiled.
        /// </summary>
        public DecoratorTypeResolver()
        {
            _generator = new Lazy<DecoratorGenerator>(() => new DecoratorGenerator(HandlebarsDecoratorCodeWriter.EmbeddedOnly(), new FsDecoratorTargetResolver()));
            _types = new ConcurrentDictionary<DecoratorKey, Lazy<Type>>();
        }

        /// <summary>Initializes a new instance that generates source with <paramref name="generator"/>.</summary>
        /// <param name="generator">
        /// Renders the source of decorators that have to be compiled at runtime. Its output is compiled and
        /// loaded, so its template sources must be trusted.
        /// </param>
        public DecoratorTypeResolver(DecoratorGenerator generator)
        {
            ArgumentNullException.ThrowIfNull(generator);

            _generator = new Lazy<DecoratorGenerator>(() => generator);
            _types = new ConcurrentDictionary<DecoratorKey, Lazy<Type>>();
        }

        /// <summary>Gets the process-wide resolver used when a registry has none of its own.</summary>
        public static DecoratorTypeResolver Default => _default.Value;

        /// <summary>Gets the generator that renders the source of decorators compiled at runtime.</summary>
        public DecoratorGenerator Generator => _generator.Value;

        /// <inheritdoc />
        public Type Resolve(Type interfaceType, Type implementationType)
        {
            ArgumentNullException.ThrowIfNull(interfaceType);
            ArgumentNullException.ThrowIfNull(implementationType);

            DecoratorKey key = new DecoratorKey(interfaceType, implementationType);
            Lazy<Type> resolution = _types.GetOrAdd(
                key,
                pair => new Lazy<Type>(() => FindGenerated(pair.InterfaceType, pair.ImplementationType) ?? Compile(pair.InterfaceType, pair.ImplementationType)));
            try
            {
                return resolution.Value;
            }
            catch (Exception)
            {
                // Lazy remembers a failure for good. Forget it, so a later attempt gets to try again.
                _types.TryRemove(new KeyValuePair<DecoratorKey, Lazy<Type>>(key, resolution));
                throw;
            }
        }

        /// <summary>
        /// Looks through the loaded assemblies for a decorator of the pair that was generated ahead of time.
        /// Only assemblies that reference the decorator runtime are searched. When more than one decorator of
        /// the pair is loaded, the one with the name the generator would give it is preferred.
        /// </summary>
        /// <param name="interfaceType">The service interface.</param>
        /// <param name="implementationType">The implementation type being decorated.</param>
        /// <returns>The decorator type, or null when none is loaded.</returns>
        protected virtual Type? FindGenerated(Type interfaceType, Type implementationType)
        {
            Type baseType;
            try
            {
                baseType = typeof(Decorator<,>).MakeGenericType(interfaceType, implementationType);
            }
            catch (ArgumentException ex)
            {
                throw new DecoratorGenerationException(interfaceType, $"{implementationType.FullName} cannot be decorated as the interface.", ex);
            }

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
                    && type.GetConstructor(new Type[] { implementationType, typeof(ILogger) }) != null));
            }

            if (candidates.Count < 2)
            {
                return candidates.FirstOrDefault();
            }

            string conventionalName = new DecoratorModel(interfaceType, implementationType).DecoratorFullName;
            return candidates.FirstOrDefault(type => type.FullName == conventionalName) ?? candidates[0];
        }

        /// <summary>Generates, compiles and loads a decorator for the pair.</summary>
        /// <param name="interfaceType">The service interface.</param>
        /// <param name="implementationType">The implementation type being decorated.</param>
        /// <exception cref="DecoratorGenerationException">The pair cannot be decorated, or the generated source did not compile.</exception>
        protected virtual Type Compile(Type interfaceType, Type implementationType)
        {
            DecoratorModel model = new DecoratorModel(interfaceType, implementationType);
            string source = Generator.CodeWriter.GetSource(model);
            Type[] references = model.ReferencedTypes.Where(type => !string.IsNullOrEmpty(type.Assembly.Location)).ToArray();

            Assembly assembly;
            try
            {
                assembly = Assembly.Load(new RoslynCompiler().Compile(model.DecoratorFullName, source, references));
            }
            catch (Exception ex)
            {
                throw new DecoratorGenerationException(interfaceType, $"The decorator generated for {implementationType.FullName} did not compile: {ex.Message}", ex);
            }

            return assembly.GetType(model.DecoratorFullName)
                ?? throw new DecoratorGenerationException(interfaceType, $"The compiled decorator assembly does not contain {model.DecoratorFullName}.");
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

        private readonly record struct DecoratorKey(Type InterfaceType, Type ImplementationType);
    }
}
