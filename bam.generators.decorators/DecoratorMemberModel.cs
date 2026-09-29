using System.Reflection;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Handlebars render model for one interface member a generated decorator implements. Derived models
    /// precompute the member's source in <see cref="RenderedMember"/>; the decorator template emits it.
    /// </summary>
    public abstract class DecoratorMemberModel
    {
        /// <summary>The indentation of a member inside the generated class.</summary>
        protected const string Indent = "        ";

        /// <summary>Initializes the model for a member declared on <paramref name="declaringInterface"/>.</summary>
        /// <param name="declaringInterface">The interface declaring the member — the service interface or one it inherits.</param>
        /// <param name="member">The reflected member.</param>
        /// <param name="isExplicit">Whether the member is rendered as an explicit interface implementation.</param>
        protected DecoratorMemberModel(Type declaringInterface, MemberInfo member, bool isExplicit)
        {
            DeclaringInterface = declaringInterface;
            Member = member;
            IsExplicit = isExplicit;
        }

        /// <summary>Gets the interface declaring the member.</summary>
        public Type DeclaringInterface { get; }

        /// <summary>Gets the reflected member.</summary>
        public MemberInfo Member { get; }

        /// <summary>Gets the member's name, as reflection reports it.</summary>
        public string Name => Member.Name;

        /// <summary>
        /// Gets the member's name as it is written in source: a name that is a C# keyword is prefixed with
        /// <c>@</c>, as it had to be where the interface declares it.
        /// </summary>
        public string IdentifierName => CSharpTypeName.Identifier(Member.Name);

        /// <summary>
        /// Gets a value indicating whether the member is rendered as an explicit interface implementation rather
        /// than a public member. Members are explicit when a public member would clash: generic methods (whose
        /// constraints cannot be restated reliably), signatures already emitted for another interface, and
        /// names already taken by the decorator base class.
        /// </summary>
        public bool IsExplicit { get; }

        /// <summary>Gets the fully-qualified name of <see cref="DeclaringInterface"/>.</summary>
        public string DeclaringInterfaceName => CSharpTypeName.Of(DeclaringInterface);

        /// <summary>Gets the expression that reaches the decorated instance through <see cref="DeclaringInterface"/>.</summary>
        protected string Target => $"(({DeclaringInterfaceName})base.Instance)";

        /// <summary>Gets <c>public </c> for a public member, or nothing for an explicit implementation.</summary>
        protected string Modifier => IsExplicit ? string.Empty : "public ";

        /// <summary>Gets the member name as declared in source: qualified by its interface when explicit.</summary>
        /// <param name="name">The simple member name (or <c>this</c> for an indexer).</param>
        protected string Qualify(string name)
        {
            return IsExplicit ? $"{DeclaringInterfaceName}.{name}" : name;
        }

        /// <summary>Gets the fully rendered source of the member (consumed by the decorator template).</summary>
        public abstract string RenderedMember { get; }

        /// <summary>Gets the types the rendered member mentions, used to reference their assemblies when compiling.</summary>
        public abstract IEnumerable<Type> ReferencedTypes { get; }

        /// <summary>Renders a parameter list with its <c>ref</c>/<c>out</c>/<c>in</c> modifiers and types.</summary>
        protected static string RenderParameters(ParameterInfo[] parameters)
        {
            return string.Join(", ", parameters.Select(parameter =>
                $"{ModifierOf(parameter)}{CSharpTypeName.Of(parameter)} {CSharpTypeName.Identifier(parameter.Name!)}"));
        }

        /// <summary>Renders an argument list that passes <paramref name="parameters"/> through, with modifiers.</summary>
        protected static string RenderArguments(ParameterInfo[] parameters)
        {
            return string.Join(", ", parameters.Select(parameter =>
                $"{ModifierOf(parameter)}{CSharpTypeName.Identifier(parameter.Name!)}"));
        }

        private static string ModifierOf(ParameterInfo parameter)
        {
            if (!parameter.ParameterType.IsByRef)
            {
                return string.Empty;
            }

            if (parameter.IsOut)
            {
                return "out ";
            }

            return parameter.IsIn ? "in " : "ref ";
        }
    }
}
