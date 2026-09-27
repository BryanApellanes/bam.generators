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

        /// <summary>Queues a service for generation. Queuing the same pair twice has no effect.</summary>
        /// <param name="interfaceType">The service interface the decorator implements.</param>
        /// <param name="implementationType">The implementation type the decorator wraps.</param>
        /// <returns>This generator, for chaining.</returns>
        public DecoratorGenerator AddServiceType(Type interfaceType, Type implementationType)
        {
            ArgumentNullException.ThrowIfNull(interfaceType);
            ArgumentNullException.ThrowIfNull(implementationType);

            _services.Add(new DecoratorRequest(interfaceType, implementationType));
            return this;
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
        /// <param name="implementationType">The implementation type the decorator wraps.</param>
        /// <exception cref="DecoratorGenerationException">The pair cannot be decorated.</exception>
        public string GetSource(Type interfaceType, Type implementationType)
        {
            return CodeWriter.GetSource(new DecoratorModel(interfaceType, implementationType));
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
        /// per service, overwriting files from a previous run.
        /// </summary>
        /// <param name="outputDirectory">The directory to write to; created if missing.</param>
        /// <returns>The paths of the files written, in the order the services were queued.</returns>
        /// <exception cref="DecoratorGenerationException">A queued pair cannot be decorated.</exception>
        public IReadOnlyList<string> WriteSource(string outputDirectory)
        {
            List<string> written = new List<string>();
            foreach (DecoratorRequest request in _services)
            {
                DecoratorModel model = new DecoratorModel(request.InterfaceType, request.ImplementationType);
                using Stream stream = TargetResolver.GetTargetDecoratorStream(null, outputDirectory, model);
                CodeWriter.WriteDecorator(model, stream);
                written.Add(Path.Combine(outputDirectory, model.FileName));
            }

            return written;
        }

        private readonly record struct DecoratorRequest(Type InterfaceType, Type ImplementationType);
    }
}
