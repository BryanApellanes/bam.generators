using Bam.DependencyInjection;
using Bam.Logging;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Handlebars render model for a generated decorator. Reflects a service interface and its implementation
    /// into the data the decorator templates consume, validating that the pair can be decorated.
    /// </summary>
    public class DecoratorModel
    {
        /// <summary>
        /// Builds the model for decorating <paramref name="implementationType"/> as
        /// <paramref name="interfaceType"/>.
        /// </summary>
        /// <param name="interfaceType">The service interface the decorator implements.</param>
        /// <param name="implementationType">The implementation type the decorator wraps.</param>
        /// <exception cref="DecoratorGenerationException">
        /// Thrown when <paramref name="interfaceType"/> is not a closed, public interface;
        /// <paramref name="implementationType"/> is not a closed, public class that implements it; or the
        /// interface declares a member generated code cannot implement (static, init-only or ref-returning).
        /// </exception>
        public DecoratorModel(Type interfaceType, Type implementationType)
        {
            ArgumentNullException.ThrowIfNull(interfaceType);
            ArgumentNullException.ThrowIfNull(implementationType);
            Validate(interfaceType, implementationType);

            InterfaceType = interfaceType;
            ImplementationType = implementationType;
            if (!SyntaxFacts.IsValidIdentifier(DecoratorTypeName))
            {
                throw new DecoratorGenerationException(interfaceType, $"'{DecoratorTypeName}' is not a valid C# type name; {implementationType.FullName} cannot be decorated.");
            }

            Methods = new List<DecoratorMethodModel>();
            Properties = new List<DecoratorPropertyModel>();
            Events = new List<DecoratorEventModel>();

            ReflectMembers();
            Hooks = BuildHooks();
        }

        /// <summary>Gets the service interface the decorator implements.</summary>
        public Type InterfaceType { get; }

        /// <summary>Gets the implementation type the decorator wraps.</summary>
        public Type ImplementationType { get; }

        /// <summary>Gets the methods of the interface and the interfaces it inherits.</summary>
        public List<DecoratorMethodModel> Methods { get; }

        /// <summary>Gets the properties and indexers of the interface and the interfaces it inherits.</summary>
        public List<DecoratorPropertyModel> Properties { get; }

        /// <summary>Gets the events of the interface and the interfaces it inherits.</summary>
        public List<DecoratorEventModel> Events { get; }

        /// <summary>Gets the typed extension-method hooks: one per intercepted method name and phase.</summary>
        public List<DecoratorHookModel> Hooks { get; }

        /// <summary>Gets the namespace of the generated decorator.</summary>
        public string Namespace => (ImplementationType.Namespace ?? "Bam.Generated") + ".Decorators";

        /// <summary>Gets the generated decorator's simple type name, e.g. <c>EchoServiceDecorator</c>.</summary>
        public string DecoratorTypeName => SimpleName(ImplementationType) + "Decorator";

        /// <summary>Gets the simple name of the generated static class holding the extension methods.</summary>
        public string ExtensionsTypeName => DecoratorTypeName + "Extensions";

        /// <summary>Gets the name of the generated extension method that decorates the service in a registry, e.g. <c>DecorateEchoService</c>.</summary>
        public string DecorateMethodName => "Decorate" + SimpleName(ImplementationType);

        /// <summary>Gets the full name of the generated decorator type, as used to find it in a compiled assembly.</summary>
        public string DecoratorFullName => Namespace + "." + DecoratorTypeName;

        /// <summary>Gets the name of the file the decorator is written to.</summary>
        public string FileName => DecoratorTypeName + ".cs";

        /// <summary>Gets the fully-qualified name of the service interface.</summary>
        public string InterfaceName => CSharpTypeName.Of(InterfaceType);

        /// <summary>Gets the fully-qualified name of the implementation type.</summary>
        public string ImplementationName => CSharpTypeName.Of(ImplementationType);

        /// <summary>Gets the fully-qualified base type of the generated decorator.</summary>
        public string BaseTypeName => $"global::Bam.Generators.Decorators.Decorator<{InterfaceName}, {ImplementationName}>";

        /// <summary>Gets the fully-qualified type of the context handed to typed handlers.</summary>
        public string ContextTypeName => $"global::Bam.Generators.Decorators.DecoratorInvocationContext<{ImplementationName}>";

        /// <summary>
        /// Gets one type from each assembly the generated source depends on, for referencing those assemblies
        /// when the source is compiled.
        /// </summary>
        public Type[] ReferencedTypes
        {
            get
            {
                List<Type> types = new List<Type>
                {
                    InterfaceType,
                    ImplementationType,
                    typeof(Decorator<>),
                    typeof(ServiceRegistry),
                    typeof(ILogger)
                };
                types.AddRange(Members.SelectMany(member => member.ReferencedTypes).SelectMany(Flatten));

                return types
                    .Where(type => !type.IsGenericParameter)
                    .GroupBy(type => type.Assembly)
                    .Select(group => group.First())
                    .ToArray();
            }
        }

        private IEnumerable<DecoratorMemberModel> Members => Properties.Cast<DecoratorMemberModel>().Concat(Events).Concat(Methods);

        private static void Validate(Type interfaceType, Type implementationType)
        {
            if (!interfaceType.IsInterface)
            {
                throw new DecoratorGenerationException(interfaceType, "Type is not an interface. A decorator stands in for a service interface.");
            }

            if (interfaceType.ContainsGenericParameters)
            {
                throw new DecoratorGenerationException(interfaceType, "Interface is an open generic type. Decorate a closed type such as IRepository<Person>.");
            }

            if (!implementationType.IsClass || implementationType.ContainsGenericParameters)
            {
                throw new DecoratorGenerationException(interfaceType, $"{implementationType.FullName} is not a closed class type.");
            }

            if (!interfaceType.IsAssignableFrom(implementationType))
            {
                throw new DecoratorGenerationException(interfaceType, $"{implementationType.FullName} does not implement the interface.");
            }

            if (!interfaceType.IsVisible || !implementationType.IsVisible)
            {
                throw new DecoratorGenerationException(interfaceType, $"The interface and {implementationType.FullName} must both be public to be referenced from generated code.");
            }
        }

        private void ReflectMembers()
        {
            // Names a public member of the generated class may not take: the class's own name, and everything
            // it inherits from the decorator base. Interface members with these names are implemented explicitly.
            HashSet<string> reserved = new HashSet<string>(
                typeof(Decorator<,>)
                    .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .Select(member => member.Name))
            {
                DecoratorTypeName
            };
            HashSet<string> emitted = new HashSet<string>();
            Dictionary<string, MemberTypes> kinds = new Dictionary<string, MemberTypes>();

            List<Type> interfaces = new List<Type> { InterfaceType };
            interfaces.AddRange(InterfaceType.GetInterfaces());
            foreach (Type declaring in interfaces)
            {
                foreach (PropertyInfo property in declaring.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    RequireSupported(property);
                    RequireIdentifiers(property.Name, property.GetIndexParameters());
                    string signature = property.GetIndexParameters().Length == 0
                        ? property.Name
                        : "this[" + SignatureOf(property.GetIndexParameters()) + "]";
                    Properties.Add(new DecoratorPropertyModel(declaring, property, IsExplicit(reserved, emitted, kinds, property, signature)));
                }

                foreach (EventInfo eventInfo in declaring.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    RequireIdentifiers(eventInfo.Name, Array.Empty<ParameterInfo>());
                    Events.Add(new DecoratorEventModel(declaring, eventInfo, IsExplicit(reserved, emitted, kinds, eventInfo, eventInfo.Name)));
                }

                foreach (MethodInfo method in declaring.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (method.IsSpecialName)
                    {
                        continue; // property and event accessors
                    }

                    RequireSupported(method);
                    RequireIdentifiers(method.Name, method.GetParameters());
                    string signature = $"{method.Name}`{method.GetGenericArguments().Length}({SignatureOf(method.GetParameters())})";
                    bool isExplicit = IsExplicit(reserved, emitted, kinds, method, signature) || method.IsGenericMethodDefinition;
                    Methods.Add(new DecoratorMethodModel(declaring, method, isExplicit));
                }
            }

            RequireNoStaticMembers(interfaces);
        }

        // A member is explicit when a public member would not compile: its name is reserved, a member with the
        // same signature has already been emitted for another interface, or a member of another kind (a property
        // Count and a method Count, from two interfaces) already took the name.
        private static bool IsExplicit(HashSet<string> reserved, HashSet<string> emitted, Dictionary<string, MemberTypes> kinds, MemberInfo member, string signature)
        {
            bool alreadyEmitted = !emitted.Add(signature);
            bool takenByAnotherKind = kinds.TryGetValue(member.Name, out MemberTypes kind) && kind != member.MemberType;
            kinds.TryAdd(member.Name, member.MemberType);
            return alreadyEmitted || takenByAnotherKind || reserved.Contains(member.Name);
        }

        // Names are written into generated source verbatim. Source compiled from C# always passes; this refuses
        // names only hand-written or obfuscated IL can carry, which would otherwise corrupt the generated file.
        private void RequireIdentifiers(string memberName, ParameterInfo[] parameters)
        {
            if (!SyntaxFacts.IsValidIdentifier(memberName))
            {
                throw new DecoratorGenerationException(InterfaceType, $"Member name '{memberName}' is not a valid C# identifier.");
            }

            foreach (ParameterInfo parameter in parameters)
            {
                if (parameter.Name == null || !SyntaxFacts.IsValidIdentifier(parameter.Name))
                {
                    throw new DecoratorGenerationException(InterfaceType, $"{memberName} has a parameter whose name is not a valid C# identifier.");
                }
            }
        }

        private static string SignatureOf(ParameterInfo[] parameters)
        {
            return string.Join(",", parameters.Select(parameter => parameter.ParameterType.ToString()));
        }

        private void RequireSupported(PropertyInfo property)
        {
            MethodInfo? setter = property.SetMethod;
            if (setter != null && setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
            {
                throw new DecoratorGenerationException(InterfaceType, $"Property {property.Name} is init-only, which a decorator cannot forward.");
            }

            if (property.PropertyType.IsByRef)
            {
                throw new DecoratorGenerationException(InterfaceType, $"Property {property.Name} returns by reference, which is not supported.");
            }
        }

        private void RequireSupported(MethodInfo method)
        {
            if (method.ReturnType.IsByRef)
            {
                throw new DecoratorGenerationException(InterfaceType, $"Method {method.Name} returns by reference, which is not supported.");
            }
        }

        private void RequireNoStaticMembers(List<Type> interfaces)
        {
            foreach (Type declaring in interfaces)
            {
                MethodInfo? member = declaring
                    .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(method => method.IsAbstract);
                if (member != null)
                {
                    throw new DecoratorGenerationException(InterfaceType, $"{declaring.Name}.{member.Name} is a static abstract member, which a decorator cannot implement.");
                }
            }
        }

        private List<DecoratorHookModel> BuildHooks()
        {
            List<DecoratorHookModel> hooks = new List<DecoratorHookModel>();
            foreach (IGrouping<string, DecoratorMethodModel> overloads in Methods.Where(method => method.IsIntercepted).GroupBy(method => method.Name))
            {
                string? resultTypeName = HandlerResultTypeName(overloads.ToList());
                foreach (DecoratorPhase phase in new DecoratorPhase[] { DecoratorPhase.Start, DecoratorPhase.End, DecoratorPhase.Error })
                {
                    hooks.Add(new DecoratorHookModel(this, overloads.Key, phase, resultTypeName));
                }
            }

            return hooks;
        }

        // The type a typed handler returns: the method's result in nullable form, so that returning null means
        // "no override". Null when the overloads do not agree on one concrete type.
        private static string? HandlerResultTypeName(List<DecoratorMethodModel> overloads)
        {
            Type? resultType = overloads[0].ResultType;
            if (resultType == null || ContainsGenericParameter(resultType) || overloads.Any(overload => overload.ResultType != resultType))
            {
                return null;
            }

            string name = overloads[0].ResultTypeName!;
            bool alreadyNullable = name.EndsWith('?') || Nullable.GetUnderlyingType(resultType) != null;
            return alreadyNullable ? name : name + "?";
        }

        private static bool ContainsGenericParameter(Type type)
        {
            return Flatten(type).Any(candidate => candidate.IsGenericParameter);
        }

        // The type itself plus everything it is built from: element types and generic arguments.
        private static IEnumerable<Type> Flatten(Type type)
        {
            if (type.HasElementType)
            {
                return Flatten(type.GetElementType()!);
            }

            if (type.IsGenericType)
            {
                return new Type[] { type }.Concat(type.GetGenericArguments().SelectMany(Flatten));
            }

            return new Type[] { type };
        }

        private static string SimpleName(Type type)
        {
            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick < 0)
            {
                return name;
            }

            return name.Substring(0, tick) + "Of" + string.Concat(type.GetGenericArguments().Select(SimpleName));
        }
    }
}
