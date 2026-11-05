namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Generates strongly-typed decorators, and the extension methods for subscribing to them, for service
    /// interfaces by rendering Handlebars templates. Mirrors the lifecycle of <c>BamServiceClientGenerator</c>:
    /// queue service types, then write them all, or render one without writing it.
    /// </summary>
    public class DecoratorGenerator
    {
        private readonly HashSet<DecoratorRequest> _services = new();

        /// <summary>Initializes a new instance with the default Handlebars writer and file-system target resolver.</summary>
        public DecoratorGenerator()
            : this(new HandlebarsDecoratorCodeWriter(), new FsDecoratorTargetResolver())
        {
        }

        /// <summary>Initializes a new instance with the specified writer and target resolver.</summary>
        /// <param name="codeWriter">Renders decorator source.</param>
        /// <param name="targetResolver">Locates the stream each decorator is written to.</param>
        public DecoratorGenerator(IDecoratorCodeWriter codeWriter, IDecoratorTargetResolver targetResolver)
        {
            CodeWriter = codeWriter;
            TargetResolver = targetResolver;
        }

        /// <summary>Gets or sets the code writer used to render decorators.</summary>
        public IDecoratorCodeWriter CodeWriter { get; set; }

        /// <summary>Gets or sets the resolver used to locate output streams.</summary>
        public IDecoratorTargetResolver TargetResolver { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether generated decorators include the <c>ServiceRegistry</c>
        /// extension methods (the <c>Decorate</c> method and the <c>this ServiceRegistry</c> hook overloads).
        /// Defaults to true. When false, the output references the <c>bam.generators.decorators.runtime</c>
        /// package alone, for hosts on another container; see <see cref="DecoratorFactory"/>.
        /// </summary>
        public bool RegistryExtensions { get; set; } = true;

        /// <summary>Generates decorators without the <c>ServiceRegistry</c> extension methods.</summary>
        /// <returns>This generator, for chaining.</returns>
        public DecoratorGenerator WithoutRegistryExtensions()
        {
            RegistryExtensions = false;
            return this;
        }

        /// <summary>Generates decorators with the <c>ServiceRegistry</c> extension methods (the default).</summary>
        /// <returns>This generator, for chaining.</returns>
        public DecoratorGenerator WithRegistryExtensions()
        {
            RegistryExtensions = true;
            return this;
        }

        /// <summary>Queues a service for generation. Queuing the same pair twice has no effect.</summary>
        /// <param name="interfaceType">The service interface the decorator implements.</param>
        /// <param name="implementationType">
        /// The implementation type the decorator wraps, or null to decorate the interface alone (see
        /// <see cref="AddInterface(Type)"/>).
        /// </param>
        /// <returns>This generator, for chaining.</returns>
        public DecoratorGenerator AddServiceType(Type interfaceType, Type? implementationType)
        {
            ArgumentNullException.ThrowIfNull(interfaceType);

            _services.Add(new DecoratorRequest(interfaceType, implementationType ?? interfaceType));
            return this;
        }

        /// <summary>
        /// Queues an interface for generation without an implementation type. The decorator is typed on the
        /// interface (<c>Decorator&lt;I, I&gt;</c>, constructor taking an <c>I</c>) and wraps any instance of
        /// it, for services whose implementation is internal or chosen at resolve time. It is named after the
        /// interface: <c>IEchoServiceDecorator</c>.
        /// </summary>
        /// <param name="interfaceType">The service interface the decorator implements.</param>
        /// <returns>This generator, for chaining.</returns>
        public DecoratorGenerator AddInterface(Type interfaceType)
        {
            return AddServiceType(interfaceType, null);
        }

        /// <summary>Queues an interface for generation without an implementation type; see <see cref="AddInterface(Type)"/>.</summary>
        /// <typeparam name="I">The service interface the decorator implements.</typeparam>
        /// <returns>This generator, for chaining.</returns>
        public DecoratorGenerator AddInterface<I>() where I : class
        {
            return AddServiceType(typeof(I), null);
        }

        /// <summary>Queues a service for generation. Queuing the same pair twice has no effect.</summary>
        /// <typeparam name="I">The service interface the decorator implements.</typeparam>
        /// <typeparam name="T">The implementation type the decorator wraps.</typeparam>
        /// <returns>This generator, for chaining.</returns>
        public DecoratorGenerator AddServiceType<I, T>() where I : class where T : class, I
        {
            return AddServiceType(typeof(I), typeof(T));
        }

        /// <summary>Renders the decorator source for a single service without writing it.</summary>
        /// <param name="interfaceType">The service interface the decorator implements.</param>
        /// <param name="implementationType">The implementation type the decorator wraps, or null to decorate the interface alone.</param>
        /// <exception cref="DecoratorGenerationException">The pair cannot be decorated.</exception>
        public string GetSource(Type interfaceType, Type? implementationType)
        {
            return CodeWriter.GetSource(new DecoratorModel(interfaceType, implementationType, RegistryExtensions));
        }

        /// <summary>Renders the source of a decorator typed on the interface alone, without writing it; see <see cref="AddInterface(Type)"/>.</summary>
        /// <typeparam name="I">The service interface the decorator implements.</typeparam>
        public string GetSource<I>() where I : class
        {
            return GetSource(typeof(I), null);
        }

        /// <summary>Renders the decorator source for a single service without writing it.</summary>
        /// <typeparam name="I">The service interface the decorator implements.</typeparam>
        /// <typeparam name="T">The implementation type the decorator wraps.</typeparam>
        public string GetSource<I, T>() where I : class where T : class, I
        {
            return GetSource(typeof(I), typeof(T));
        }

        /// <summary>
        /// Writes the decorator source for every queued service to <paramref name="outputDirectory"/>, one file
        /// per service, overwriting files from a previous run. Every queued service is validated and the
        /// decorator names are checked for a clash before the first file is written, so a refused run writes
        /// nothing.
        /// </summary>
        /// <param name="outputDirectory">The directory to write to; created if missing.</param>
        /// <returns>The paths of the files written, in the order the services were queued.</returns>
        /// <exception cref="DecoratorGenerationException">
        /// A queued pair cannot be decorated, or two queued services would generate decorators of the same name
        /// (two types named alike in one namespace), which would overwrite each other.
        /// </exception>
        public IReadOnlyList<string> WriteSource(string outputDirectory)
        {
            Dictionary<string, DecoratorModel> byName = new Dictionary<string, DecoratorModel>(StringComparer.Ordinal);
            List<DecoratorModel> models = new List<DecoratorModel>();
            foreach (DecoratorRequest request in _services)
            {
                DecoratorModel model = new DecoratorModel(request.InterfaceType, request.ImplementationType, RegistryExtensions);
                if (byName.TryGetValue(model.DecoratorFullName, out DecoratorModel? other))
                {
                    throw new DecoratorGenerationException(
                        request.InterfaceType,
                        $"Decorating {request.ImplementationType.FullName} would generate {model.DecoratorFullName}, the same decorator as {other.ImplementationType.FullName}; the two files would overwrite each other.");
                }

                byName.Add(model.DecoratorFullName, model);
                models.Add(model);
            }

            List<string> written = new List<string>();
            foreach (DecoratorModel model in models)
            {
                using Stream stream = TargetResolver.GetTargetDecoratorStream(null, outputDirectory, model);
                CodeWriter.WriteDecorator(model, stream);
                written.Add(Path.Combine(outputDirectory, model.FileName));
            }

            return written;
        }

        private readonly record struct DecoratorRequest(Type InterfaceType, Type ImplementationType);
    }
}
