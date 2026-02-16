using Bam.Data.Repositories;

namespace Bam.Generators
{
    /// <summary>
    /// Extends <see cref="SchemaTypeModel"/> with Handlebars-specific properties such as pluralized type names
    /// for use in template rendering.
    /// </summary>
    public class HandlebarsSchemaTypeModel: SchemaTypeModel
    {
        /// <summary>
        /// Gets the pluralized form of the type name.
        /// </summary>
        public string TypeNamePluralized
        {
            get
            {
                return Type.Name.Pluralize();
            }
        }

        /// <summary>
        /// Creates a new <see cref="HandlebarsSchemaTypeModel"/> from the specified CLR type and DAO namespace.
        /// </summary>
        /// <param name="type">The CLR type to create the model from.</param>
        /// <param name="daoNamespace">The target DAO namespace.</param>
        /// <returns>A new <see cref="HandlebarsSchemaTypeModel"/> instance.</returns>
        public new static HandlebarsSchemaTypeModel FromType(Type type, string daoNamespace)
        {
            return new HandlebarsSchemaTypeModel { Type = type, DaoNamespace = daoNamespace };
        }
    }
}
