using System.Reflection;
using Bam.Data.Repositories;

namespace Bam.Generators
{
    /// <summary>
    /// Extends <see cref="TypeFk"/> with Handlebars template-friendly properties for foreign key relationships,
    /// providing camelCase names and type string representations.
    /// </summary>
    public class HandlebarsTypeFkModel: TypeFk
    {
        /// <summary>
        /// Gets the primary key type name in camelCase format.
        /// </summary>
        public string PrimaryKeyTypeCamelCaseName
        {
            get { return PrimaryKeyType.Name.CamelCase(); }
        }

        /// <summary>
        /// Gets a value indicating whether the child's property that references its parent is declared nullable,
        /// so the generated override can carry the same annotation.
        /// </summary>
        public bool ChildParentPropertyIsNullable
        {
            get
            {
                if (ChildParentProperty == null || ChildParentProperty.PropertyType.IsValueType)
                {
                    return false;
                }
                NullabilityInfoContext context = new NullabilityInfoContext();
                return context.Create(ChildParentProperty).ReadState == NullabilityState.Nullable;
            }
        }

        /// <summary>
        /// Gets the collection property name in camelCase format.
        /// </summary>
        public string CollectionPropertyCamelCaseName
        {
            get
            {
                return CollectionProperty.Name.CamelCase();
            }
        }

        /// <summary>
        /// Gets the full type string representation of the collection property's type, including generic arguments.
        /// </summary>
        public string CollectionPropertyTypeString
        {
            get
            {
                return CollectionProperty.PropertyType.ToTypeString(true);
            }
        }

        /// <summary>
        /// Gets "Array" if the collection property type is an array, or "List" otherwise.
        /// </summary>
        public string CollectionPropertyTypeName
        {
            get
            {
                return CollectionProperty.PropertyType.IsArray ? "Array" : "List";
            }
        }
    }
}
