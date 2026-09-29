using System.Reflection;
using System.Text;

namespace Bam.Generators.Decorators
{
    /// <summary>
    /// Handlebars render model for one interface property or indexer. Properties are forwarded straight to the
    /// decorated instance; handlers do not run for them.
    /// </summary>
    public class DecoratorPropertyModel : DecoratorMemberModel
    {
        /// <summary>Initializes the model by reflecting over <paramref name="property"/>.</summary>
        /// <param name="declaringInterface">The interface declaring the property.</param>
        /// <param name="property">The reflected property.</param>
        /// <param name="isExplicit">Whether to render an explicit interface implementation.</param>
        public DecoratorPropertyModel(Type declaringInterface, PropertyInfo property, bool isExplicit)
            : base(declaringInterface, property, isExplicit)
        {
            Property = property;
            IndexParameters = property.GetIndexParameters();
        }

        /// <summary>Gets the reflected property.</summary>
        public PropertyInfo Property { get; }

        /// <summary>Gets the index parameters; empty unless the property is an indexer.</summary>
        public ParameterInfo[] IndexParameters { get; }

        /// <summary>Gets a value indicating whether the property is an indexer.</summary>
        public bool IsIndexer => IndexParameters.Length > 0;

        /// <inheritdoc />
        public override IEnumerable<Type> ReferencedTypes
        {
            get
            {
                yield return DeclaringInterface;
                yield return Property.PropertyType;
                foreach (ParameterInfo parameter in IndexParameters)
                {
                    yield return parameter.ParameterType;
                }
            }
        }

        /// <inheritdoc />
        public override string RenderedMember
        {
            get
            {
                string declaration = IsIndexer ? $"{Qualify("this")}[{RenderParameters(IndexParameters)}]" : Qualify(IdentifierName);
                string access = IsIndexer ? $"{Target}[{RenderArguments(IndexParameters)}]" : $"{Target}.{IdentifierName}";

                StringBuilder source = new StringBuilder();
                source.AppendLine($"{Indent}/// <inheritdoc />");
                source.AppendLine($"{Indent}{Modifier}{CSharpTypeName.Of(Property)} {declaration}");
                source.AppendLine($"{Indent}{{");
                if (Property.GetMethod != null)
                {
                    source.AppendLine($"{Indent}    get => {access};");
                }
                if (Property.SetMethod != null)
                {
                    source.AppendLine($"{Indent}    set => {access} = value;");
                }
                source.AppendLine($"{Indent}}}");
                return source.ToString().TrimEnd();
            }
        }
    }
}
