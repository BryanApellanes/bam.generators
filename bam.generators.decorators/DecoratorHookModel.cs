namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Handlebars render model for the typed extension methods generated for one intercepted method in one
    /// phase — for a method <c>Message</c> at <see cref="DecoratorPhase.Start"/>, the <c>OnMessageStart</c>
    /// overloads. Overloads of a method share one hook, since handlers are selected by method name.
    /// </summary>
    public class DecoratorHookModel
    {
        /// <summary>Initializes the model for <paramref name="methodName"/> in <paramref name="phase"/>.</summary>
        /// <param name="decorator">The decorator the hook belongs to.</param>
        /// <param name="methodName">The intercepted method's name.</param>
        /// <param name="phase">The phase the hook subscribes to.</param>
        /// <param name="resultTypeName">
        /// The nullable form of the type the method produces, or null when a typed handler cannot be offered:
        /// the method produces no value, its overloads disagree, or the type is a generic parameter.
        /// </param>
        public DecoratorHookModel(DecoratorModel decorator, string methodName, DecoratorPhase phase, string? resultTypeName)
        {
            MethodName = methodName;
            Phase = phase.ToString();
            ResultTypeName = resultTypeName ?? string.Empty;
            HasResult = resultTypeName != null;
            ContextTypeName = decorator.ContextTypeName;
            InterfaceName = decorator.InterfaceName;
            ImplementationName = decorator.ImplementationName;
            DecorateMethodName = decorator.DecorateMethodName;
            ExtensionsTypeName = decorator.ExtensionsTypeName;

            switch (phase)
            {
                case DecoratorPhase.Start:
                    When = $"before {methodName} is invoked";
                    Effect = "short-circuits the invocation and is handed to the caller instead";
                    break;
                case DecoratorPhase.End:
                    When = $"after {methodName} returns";
                    Effect = "replaces the value handed to the caller";
                    break;
                default:
                    When = $"when {methodName} throws";
                    Effect = "is handed to the caller as a fallback and suppresses the exception";
                    break;
            }
        }

        /// <summary>Gets the intercepted method's name.</summary>
        public string MethodName { get; }

        /// <summary>Gets the name of the <see cref="DecoratorPhase"/> the hook subscribes to.</summary>
        public string Phase { get; }

        /// <summary>Gets the name of the generated extension methods, e.g. <c>OnMessageStart</c>.</summary>
        public string HookName => $"On{MethodName}{Phase}";

        /// <summary>Gets a value indicating whether a handler returning a typed value is offered.</summary>
        public bool HasResult { get; }

        /// <summary>Gets the nullable form of the type a typed handler returns; empty when <see cref="HasResult"/> is false.</summary>
        public string ResultTypeName { get; }

        /// <summary>Gets the fully-qualified type of the context handed to handlers.</summary>
        public string ContextTypeName { get; }

        /// <summary>Gets the fully-qualified name of the service interface.</summary>
        public string InterfaceName { get; }

        /// <summary>Gets the fully-qualified name of the implementation type.</summary>
        public string ImplementationName { get; }

        /// <summary>Gets the name of the generated extension method that decorates the service in a registry.</summary>
        public string DecorateMethodName { get; }

        /// <summary>
        /// Gets the simple name of the generated extensions class. Hooks call its decorate method by this name
        /// rather than as an extension, so the call stays unambiguous when another assembly in scope declares
        /// a decorator for the same service.
        /// </summary>
        public string ExtensionsTypeName { get; }

        /// <summary>Gets the phrase describing when the handler runs, for the generated documentation.</summary>
        public string When { get; }

        /// <summary>Gets the phrase describing what a returned value does, for the generated documentation.</summary>
        public string Effect { get; }
    }
}
