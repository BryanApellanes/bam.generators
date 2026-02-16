using Bam.Data.Repositories;

namespace Bam.Generators
{
    /// <summary>
    /// A wrapper model that converts foreign key and cross-reference relationships to their Handlebars-specific
    /// model types (<see cref="HandlebarsTypeFkModel"/> and <see cref="HandlebarsTypeXrefModel"/>) and renders
    /// using the "Wrapper" Handlebars template.
    /// </summary>
    public class HandlebarsWrapperModel : WrapperModel
    {
        /// <summary>
        /// Initializes a new instance, converting the base model's FK and Xref collections to Handlebars-specific model types.
        /// </summary>
        /// <param name="pocoType">The POCO type to generate a wrapper for.</param>
        /// <param name="schema">The type schema describing the relationships.</param>
        /// <param name="wrapperNamespace">The namespace for generated wrapper classes.</param>
        /// <param name="daoNameSpace">The namespace for the corresponding DAO classes.</param>
        public HandlebarsWrapperModel(Type pocoType, ITypeSchema schema, string wrapperNamespace = "TypeWrappers", string daoNameSpace = "Daos") : base(pocoType, schema, wrapperNamespace, daoNameSpace)
        {
            ForeignKeys = ForeignKeys.Select(fk => fk.CopyAs<HandlebarsTypeFkModel>()).ToArray();
            ChildPrimaryKeys = ChildPrimaryKeys.Select(fk => fk.CopyAs<HandlebarsTypeFkModel>()).ToArray();
            LeftXrefs = LeftXrefs.Select(lxref => lxref.CopyAs<HandlebarsTypeXrefModel>()).ToArray();
            RightXrefs = RightXrefs.Select(rx => rx.CopyAs<HandlebarsTypeXrefModel>()).ToArray();

            TemplateRenderer = new HandlebarsTemplateRenderer();
        }

        /// <summary>
        /// Renders this wrapper model using the "Wrapper" Handlebars template.
        /// </summary>
        /// <returns>The rendered wrapper C# source code as a string.</returns>
        public override string Render()
        {
            return TemplateRenderer.Render("Wrapper", this);
        }
    }
}